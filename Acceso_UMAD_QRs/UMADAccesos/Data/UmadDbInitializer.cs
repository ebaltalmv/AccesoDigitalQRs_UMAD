using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;
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
                await ReconciliarEsquemaFase2Async(db, cancellationToken).ConfigureAwait(false);

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

        /// <summary>
        /// Reconcilia las tablas y columnas de Fase 2 en bases creadas por Fase 1 mediante EnsureCreated, sin eliminar datos existentes.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>26/11/2026</date>
        /// <version>1.0</version>
        /// <modification>26/11/2026</modification>
        private static async Task ReconciliarEsquemaFase2Async(
            UmadDbContext db,
            CancellationToken cancellationToken)
        {
            await AgregarColumnaSiFaltaAsync(db, "RegistrosAcceso", "RutaEvidenciaFoto", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            await AgregarColumnaSiFaltaAsync(db, "RegistrosAcceso", "HashEvidenciaFoto", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            await AgregarColumnaSiFaltaAsync(db, "RegistrosAcceso", "GuardiaPersonaId", "INTEGER NULL", cancellationToken).ConfigureAwait(false);
            await AgregarColumnaSiFaltaAsync(db, "RegistrosAcceso", "EntradaRelacionadaId", "INTEGER NULL", cancellationToken).ConfigureAwait(false);

            await CrearTablaSiFaltaAsync(db, "SolicitudesRenovacion", """
                CREATE TABLE "SolicitudesRenovacion" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_SolicitudesRenovacion" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "PersonaId" INTEGER NOT NULL,
                    "TokenAccesoOriginalId" INTEGER NULL,
                    "TipoPaseDeseado" INTEGER NOT NULL,
                    "VigenciaSolicitada" INTEGER NULL,
                    "Justificacion" TEXT NOT NULL,
                    "Estado" INTEGER NOT NULL DEFAULT 0,
                    "MotivoResolucion" TEXT NULL,
                    "ResueltoPorPersonaId" INTEGER NULL,
                    "TokenAccesoNuevoId" INTEGER NULL,
                    "FechaSolicitudUtc" TEXT NOT NULL,
                    "FechaResolucionUtc" TEXT NULL,
                    "Sincronizado" INTEGER NOT NULL DEFAULT 0,
                    "EstadoSincronizacion" INTEGER NOT NULL DEFAULT 0,
                    "FechaUltimoIntento" TEXT NULL,
                    "Reintentos" INTEGER NOT NULL DEFAULT 0,
                    "UltimoError" TEXT NULL,
                    CONSTRAINT "FK_SolicitudesRenovacion_Personas_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_SolicitudesRenovacion_Personas_ResueltoPorPersonaId" FOREIGN KEY ("ResueltoPorPersonaId") REFERENCES "Personas" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_SolicitudesRenovacion_TokensAcceso_TokenAccesoOriginalId" FOREIGN KEY ("TokenAccesoOriginalId") REFERENCES "TokensAcceso" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_SolicitudesRenovacion_TokensAcceso_TokenAccesoNuevoId" FOREIGN KEY ("TokenAccesoNuevoId") REFERENCES "TokensAcceso" ("Id") ON DELETE SET NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await CrearTablaSiFaltaAsync(db, "Incidencias", """
                CREATE TABLE "Incidencias" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Incidencias" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "ReportadoPorPersonaId" INTEGER NOT NULL,
                    "RegistroAccesoId" INTEGER NULL,
                    "Severidad" INTEGER NOT NULL,
                    "Estado" INTEGER NOT NULL DEFAULT 0,
                    "Titulo" TEXT NOT NULL,
                    "Descripcion" TEXT NOT NULL,
                    "RutaEvidenciaFoto" TEXT NULL,
                    "HashEvidenciaFoto" TEXT NULL,
                    "FechaReporteUtc" TEXT NOT NULL,
                    "FechaResolucionUtc" TEXT NULL,
                    "Sincronizado" INTEGER NOT NULL DEFAULT 0,
                    "EstadoSincronizacion" INTEGER NOT NULL DEFAULT 0,
                    "FechaUltimoIntento" TEXT NULL,
                    "Reintentos" INTEGER NOT NULL DEFAULT 0,
                    "UltimoError" TEXT NULL,
                    CONSTRAINT "FK_Incidencias_Personas_ReportadoPorPersonaId" FOREIGN KEY ("ReportadoPorPersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_Incidencias_RegistrosAcceso_RegistroAccesoId" FOREIGN KEY ("RegistroAccesoId") REFERENCES "RegistrosAcceso" ("Id") ON DELETE SET NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await CrearTablaSiFaltaAsync(db, "PasesEvento", """
                CREATE TABLE "PasesEvento" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PasesEvento" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "NombreEvento" TEXT NOT NULL,
                    "Descripcion" TEXT NULL,
                    "FechaInicioEvento" TEXT NOT NULL,
                    "FechaFinEvento" TEXT NOT NULL,
                    "PersonaId" INTEGER NOT NULL,
                    "TokenAccesoId" INTEGER NULL,
                    "CreadoPorPersonaId" INTEGER NOT NULL,
                    "FechaCreacionUtc" TEXT NOT NULL,
                    "Sincronizado" INTEGER NOT NULL DEFAULT 0,
                    "EstadoSincronizacion" INTEGER NOT NULL DEFAULT 0,
                    "FechaUltimoIntento" TEXT NULL,
                    "Reintentos" INTEGER NOT NULL DEFAULT 0,
                    "UltimoError" TEXT NULL,
                    CONSTRAINT "FK_PasesEvento_Personas_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PasesEvento_Personas_CreadoPorPersonaId" FOREIGN KEY ("CreadoPorPersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PasesEvento_TokensAcceso_TokenAccesoId" FOREIGN KEY ("TokenAccesoId") REFERENCES "TokensAcceso" ("Id") ON DELETE SET NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await EjecutarIndicesFase2Async(db, cancellationToken).ConfigureAwait(false);
        }

        // Agrega una columna a una tabla existente solo si no fue creada previamente por otra fase.
        private static async Task AgregarColumnaSiFaltaAsync(
            UmadDbContext db,
            string tabla,
            string columna,
            string definicion,
            CancellationToken cancellationToken)
        {
            bool existeTabla = await ExisteObjetoAsync(db, tabla, "table", cancellationToken).ConfigureAwait(false);
            if (!existeTabla)
            {
                return;
            }

            if (await ExisteColumnaAsync(db, tabla, columna, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            string sentenciaAlter = "ALTER TABLE \"" + tabla + "\" ADD COLUMN \"" + columna + "\" " + definicion;
            await db.Database.ExecuteSqlRawAsync(sentenciaAlter, cancellationToken).ConfigureAwait(false);
        }

        // Crea una tabla de Fase 2 sin afectar tablas o datos de Fase 1.
        private static async Task CrearTablaSiFaltaAsync(
            UmadDbContext db,
            string tabla,
            string sentenciaCreate,
            CancellationToken cancellationToken)
        {
            if (await ExisteObjetoAsync(db, tabla, "table", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await db.Database.ExecuteSqlRawAsync(sentenciaCreate, cancellationToken).ConfigureAwait(false);
        }

        // Comprueba existencia de tablas e índices mediante sqlite_master.
        private static async Task<bool> ExisteObjetoAsync(
            UmadDbContext db,
            string nombre,
            string tipo,
            CancellationToken cancellationToken)
        {
            DbConnection conexion = db.Database.GetDbConnection();
            if (conexion.State != System.Data.ConnectionState.Open)
            {
                await conexion.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            await using DbCommand comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = $tipo AND name = $nombre;";
            DbParameter parametroTipo = comando.CreateParameter();
            parametroTipo.ParameterName = "$tipo";
            parametroTipo.Value = tipo;
            comando.Parameters.Add(parametroTipo);
            DbParameter parametroNombre = comando.CreateParameter();
            parametroNombre.ParameterName = "$nombre";
            parametroNombre.Value = nombre;
            comando.Parameters.Add(parametroNombre);

            object? resultado = await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return resultado is not null && Convert.ToInt64(resultado) > 0;
        }

        // Comprueba si una columna existe dentro de una tabla SQLite.
        private static async Task<bool> ExisteColumnaAsync(
            UmadDbContext db,
            string tabla,
            string columna,
            CancellationToken cancellationToken)
        {
            DbConnection conexion = db.Database.GetDbConnection();
            if (conexion.State != System.Data.ConnectionState.Open)
            {
                await conexion.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            await using DbCommand comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabla.Replace("'", "''")}') WHERE name = $columna;";
            DbParameter parametro = comando.CreateParameter();
            parametro.ParameterName = "$columna";
            parametro.Value = columna;
            comando.Parameters.Add(parametro);

            object? resultado = await comando.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return resultado is not null && Convert.ToInt64(resultado) > 0;
        }

        // Crea los índices operativos de Fase 2 sin alterar registros existentes.
        private static async Task EjecutarIndicesFase2Async(
            UmadDbContext db,
            CancellationToken cancellationToken)
        {
            string[] indices =
            [
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_IdentificadorGlobal" ON "SolicitudesRenovacion" ("IdentificadorGlobal");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_PersonaId" ON "SolicitudesRenovacion" ("PersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_Estado" ON "SolicitudesRenovacion" ("Estado");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_Sincronizado" ON "SolicitudesRenovacion" ("Sincronizado");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_EstadoSincronizacion" ON "SolicitudesRenovacion" ("EstadoSincronizacion");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Incidencias_IdentificadorGlobal" ON "Incidencias" ("IdentificadorGlobal");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_ReportadoPorPersonaId" ON "Incidencias" ("ReportadoPorPersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_RegistroAccesoId" ON "Incidencias" ("RegistroAccesoId");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_Estado" ON "Incidencias" ("Estado");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_Severidad" ON "Incidencias" ("Severidad");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_Sincronizado" ON "Incidencias" ("Sincronizado");""",
                """CREATE INDEX IF NOT EXISTS "IX_Incidencias_EstadoSincronizacion" ON "Incidencias" ("EstadoSincronizacion");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_PasesEvento_IdentificadorGlobal" ON "PasesEvento" ("IdentificadorGlobal");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_PersonaId" ON "PasesEvento" ("PersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_CreadoPorPersonaId" ON "PasesEvento" ("CreadoPorPersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_TokenAccesoId" ON "PasesEvento" ("TokenAccesoId");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_VentanaVigencia" ON "PasesEvento" ("FechaInicioEvento", "FechaFinEvento");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_Sincronizado" ON "PasesEvento" ("Sincronizado");""",
                """CREATE INDEX IF NOT EXISTS "IX_PasesEvento_EstadoSincronizacion" ON "PasesEvento" ("EstadoSincronizacion");""",
                """CREATE INDEX IF NOT EXISTS "IX_RegistrosAcceso_GuardiaPersonaId" ON "RegistrosAcceso" ("GuardiaPersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_RegistrosAcceso_EntradaRelacionadaId" ON "RegistrosAcceso" ("EntradaRelacionadaId");"""
            ];

            foreach (string indice in indices)
            {
                await db.Database.ExecuteSqlRawAsync(indice, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
