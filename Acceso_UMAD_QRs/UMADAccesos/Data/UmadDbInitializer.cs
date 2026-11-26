using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UMADAccesos.Models;
using UMADAccesos.Services.Security;

namespace UMADAccesos.Data
{
    /// <summary>
    /// Inicializa el esquema SQLite local y realiza la siembra segura de cuentas iniciales con hashes PBKDF2-SHA256.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public sealed class UmadDbInitializer : IUmadDbInitializer
    {
        // Fábrica de contextos para crear instancias aisladas y seguras en concurrencia.
        private readonly IDbContextFactory<UmadDbContext> _dbContextFactory;

        // Servicio criptográfico encargado de derivar hashes de contraseñas sin almacenar texto plano.
        private readonly IPasswordHasher _passwordHasher;

        // Registrador de eventos para diagnóstico de inicialización.
        private readonly ILogger<UmadDbInitializer> _logger;

        // Semáforo para serializar la inicialización y evitar condiciones de carrera al arranque.
        private readonly SemaphoreSlim _bloqueoInicializacion = new(1, 1);

        // Indica si el esquema y la siembra base ya se ejecutaron con éxito.
        public bool EstaInicializada { get; private set; }

        // Fecha y hora UTC de finalización de la inicialización.
        public DateTime? FechaInicializacionUtc { get; private set; }

        /// <summary>
        /// Inicializa una nueva instancia del inicializador de base de datos local.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public UmadDbInitializer(
            IDbContextFactory<UmadDbContext> dbContextFactory,
            IPasswordHasher passwordHasher,
            ILogger<UmadDbInitializer> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Crea el esquema de SQLite si no existe y siembra las cuentas mínimas operativas únicamente cuando la tabla está vacía.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (EstaInicializada)
            {
                return;
            }

            await _bloqueoInicializacion.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (EstaInicializada)
                {
                    return;
                }

                await using UmadDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

                bool existenPersonas = await db.Personas.AnyAsync(cancellationToken).ConfigureAwait(false);
                if (!existenPersonas)
                {
                    DateTime ahoraUtc = DateTime.UtcNow;

                    Persona[] personasIniciales =
                    [
                        new Persona
                        {
                            IdentificadorGlobal = Guid.NewGuid(),
                            Matricula = "ADM-0001",
                            NombreCompleto = "Administrador General UMAD",
                            Correo = "admin@umad.edu.mx",
                            PasswordHash = _passwordHasher.HashPassword("UmadAdmin#2026!"),
                            Rol = RolPersona.Administrador,
                            Activo = true,
                            FechaCreacionUtc = ahoraUtc
                        },
                        new Persona
                        {
                            IdentificadorGlobal = Guid.NewGuid(),
                            Matricula = "GRD-0001",
                            NombreCompleto = "Guardia Caseta Principal",
                            Correo = "guardia@umad.edu.mx",
                            PasswordHash = _passwordHasher.HashPassword("UmadGuardia#2026!"),
                            Rol = RolPersona.Guardia,
                            Activo = true,
                            FechaCreacionUtc = ahoraUtc
                        },
                        new Persona
                        {
                            IdentificadorGlobal = Guid.NewGuid(),
                            Matricula = "EST-2026001",
                            NombreCompleto = "Estudiante Demo UMAD",
                            Correo = "estudiante@umad.edu.mx",
                            PasswordHash = _passwordHasher.HashPassword("UmadEstudiante#2026!"),
                            Rol = RolPersona.Estudiante,
                            Activo = true,
                            FechaCreacionUtc = ahoraUtc
                        }
                    ];

                    await db.Personas.AddRangeAsync(personasIniciales, cancellationToken).ConfigureAwait(false);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Base de datos local inicializada con {Cantidad} personas semilla.", personasIniciales.Length);
                }

                EstaInicializada = true;
                FechaInicializacionUtc = DateTime.UtcNow;
            }
            finally
            {
                _bloqueoInicializacion.Release();
            }
        }
    }
}
