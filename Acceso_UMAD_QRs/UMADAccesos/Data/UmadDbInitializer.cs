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
                await ReconciliarEsquemaFase1Async(db, cancellationToken).ConfigureAwait(false);
                await ReconciliarEsquemaFase2Async(db, cancellationToken).ConfigureAwait(false);

#if DEBUG || UMAD_DEMO_MODE
                await SembrarDatosDesarrolloAsync(db, cancellationToken).ConfigureAwait(false);
#else
                _logger.LogInformation("Compilación Release detectada: la siembra de cuentas y escenarios demo está deshabilitada por política de seguridad.");
#endif

                EstaInicializada = true;
                FechaInicializacionUtc = DateTime.UtcNow;
            }
            finally
            {
                _bloqueoInicializacion.Release();
            }
        }

#if DEBUG || UMAD_DEMO_MODE
        /// <summary>
        /// Siembra de manera idempotente cuentas base y un escenario normal + anómalo exclusivamente en compilaciones DEBUG o UMAD_DEMO_MODE para pruebas y demostración.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        private async Task SembrarDatosDesarrolloAsync(
            UmadDbContext db,
            CancellationToken cancellationToken)
        {
            DateTime ahoraUtc = DateTime.UtcNow;

            bool existenPersonas = await db.Personas.AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!existenPersonas)
            {
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
                    },
                    new Persona
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        Matricula = "VIS-2026001",
                        NombreCompleto = "Visitante Demo Anomalía",
                        Correo = "visitante.demo@umad.edu.mx",
                        PasswordHash = _passwordHasher.HashPassword("UmadVisitante#2026!"),
                        Rol = RolPersona.Visitante,
                        Activo = true,
                        FechaCreacionUtc = ahoraUtc
                    }
                ];

                await db.Personas.AddRangeAsync(personasIniciales, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Base de datos local inicializada en DEBUG con {Cantidad} personas semilla.", personasIniciales.Length);
            }

            // Garantizar persona visitante demo en bases creadas previamente durante Fases 1-2.
            Persona? visitanteAnomalo = await db.Personas
                .FirstOrDefaultAsync(p => p.Correo == "visitante.demo@umad.edu.mx", cancellationToken)
                .ConfigureAwait(false);

            if (visitanteAnomalo is null)
            {
                visitanteAnomalo = new Persona
                {
                    IdentificadorGlobal = Guid.NewGuid(),
                    Matricula = "VIS-2026001",
                    NombreCompleto = "Visitante Demo Anomalía",
                    Correo = "visitante.demo@umad.edu.mx",
                    PasswordHash = _passwordHasher.HashPassword("UmadVisitante#2026!"),
                    Rol = RolPersona.Visitante,
                    Activo = true,
                    FechaCreacionUtc = ahoraUtc
                };

                db.Personas.Add(visitanteAnomalo);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            // Sembrar registros semilla (1 caso normal + 1 caso anómalo real) únicamente si no hay historial aún.
            bool existenRegistros = await db.RegistrosAcceso.AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!existenRegistros)
            {
                Persona? guardia = await db.Personas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Correo == "guardia@umad.edu.mx", cancellationToken)
                    .ConfigureAwait(false);

                Persona? estudiante = await db.Personas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Correo == "estudiante@umad.edu.mx", cancellationToken)
                    .ConfigureAwait(false);

                TimeZoneInfo zonaLocal = ResolverZonaHorariaInstitucional();
                DateTime ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zonaLocal);
                DateTime inicioDiaLocal = DateTime.SpecifyKind(ahoraLocal.Date, DateTimeKind.Unspecified);
                DateTime inicioDiaLocalUtc = TimeZoneInfo.ConvertTimeToUtc(inicioDiaLocal, zonaLocal);
                DateTime ayerLocal = inicioDiaLocal.AddDays(-1);
                DateTime entradaNormalUtc = TimeZoneInfo.ConvertTimeToUtc(ayerLocal.AddHours(8).AddMinutes(30), zonaLocal);
                DateTime salidaNormalUtc = TimeZoneInfo.ConvertTimeToUtc(ayerLocal.AddHours(13).AddMinutes(30), zonaLocal);

                // Caso normal: entrada a las 08:30 local y salida vinculada a las 13:30 local del día hábil previo (siempre en el pasado).
                if (estudiante is not null)
                {
                    RegistroAcceso entradaNormal = new()
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = estudiante.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = "Acceso normal verificado (semilla demo Fase 3).",
                        FechaHora = entradaNormalUtc,
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = true,
                        EstadoSincronizacion = EstadoSincronizacion.Sincronizado
                    };

                    db.RegistrosAcceso.Add(entradaNormal);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    RegistroAcceso salidaNormal = new()
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = estudiante.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Salida,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = $"Salida vinculada a entrada #{entradaNormal.Id}.",
                        FechaHora = salidaNormalUtc,
                        GuardiaPersonaId = guardia?.Id,
                        EntradaRelacionadaId = entradaNormal.Id,
                        Sincronizado = true,
                        EstadoSincronizacion = EstadoSincronizacion.Sincronizado
                    };

                    db.RegistrosAcceso.Add(salidaNormal);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                // Caso anómalo real: 4 entradas en el día local actual estrictamente en el pasado (incluyendo madrugada y entrada abierta sin salida) + 2 intentos denegados recientes.
                DateTime madrugadaLocalUtc = TimeZoneInfo.ConvertTimeToUtc(inicioDiaLocal.AddHours(2).AddMinutes(15), zonaLocal);
                DateTime techoAnomaloUtc = ahoraUtc.AddMinutes(-12);
                DateTime entrada1Utc = madrugadaLocalUtc < techoAnomaloUtc
                    ? madrugadaLocalUtc
                    : (inicioDiaLocalUtc < techoAnomaloUtc ? inicioDiaLocalUtc.AddMinutes(1) : ahoraUtc.AddMinutes(-20));
                TimeSpan ventanaDisponible = techoAnomaloUtc > entrada1Utc
                    ? techoAnomaloUtc - entrada1Utc
                    : TimeSpan.FromMinutes(12);
                DateTime entrada2Utc = entrada1Utc.AddTicks(ventanaDisponible.Ticks / 3);
                DateTime entrada3Utc = entrada1Utc.AddTicks((ventanaDisponible.Ticks * 2) / 3);
                DateTime entrada4Utc = techoAnomaloUtc > entrada3Utc ? techoAnomaloUtc : entrada3Utc.AddMinutes(1);

                RegistroAcceso[] registrosAnomalos =
                [
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Acceso Vehicular Norte",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = "Entrada fuera de horario institucional (semilla demo anómala).",
                        FechaHora = entrada1Utc,
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    },
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = "Entrada repetida #2 del mismo día (semilla demo anómala).",
                        FechaHora = entrada2Utc,
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    },
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = "Entrada repetida #3 del mismo día (semilla demo anómala).",
                        FechaHora = entrada3Utc,
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    },
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Acceso Edificio Rectoría",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Autorizado,
                        MotivoResultado = "Entrada repetida #4 abierta sin salida (semilla demo anómala).",
                        FechaHora = entrada4Utc,
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    },
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Denegado,
                        MotivoResultado = "Token expirado o revocado en intento reiterado #1 (semilla demo).",
                        FechaHora = ahoraUtc.AddMinutes(-10),
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    },
                    new RegistroAcceso
                    {
                        IdentificadorGlobal = Guid.NewGuid(),
                        PersonaId = visitanteAnomalo.Id,
                        PuntoAcceso = "Caseta Principal",
                        TipoMovimiento = TipoMovimientoAcceso.Entrada,
                        Resultado = ResultadoAcceso.Denegado,
                        MotivoResultado = "Token revocado en intento reiterado #2 (semilla demo).",
                        FechaHora = ahoraUtc.AddMinutes(-4),
                        GuardiaPersonaId = guardia?.Id,
                        Sincronizado = false,
                        EstadoSincronizacion = EstadoSincronizacion.Pendiente
                    }
                ];

                await db.RegistrosAcceso.AddRangeAsync(registrosAnomalos, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Escenarios semilla de Fase 3 creados en DEBUG (caso normal y caso anómalo).");
            }
        }
        /// <summary>
        /// Resuelve la zona horaria institucional de la UMAD con respaldo determinista a UTC-6 para fechas semilla.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        private static TimeZoneInfo ResolverZonaHorariaInstitucional()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("UMAD-UTC-6", TimeSpan.FromHours(-6), "UTC-6", "UTC-6");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("UMAD-UTC-6", TimeSpan.FromHours(-6), "UTC-6", "UTC-6");
            }
        }
#endif

        /// <summary>
        /// Garantiza la existencia de las tablas núcleo de Fase 1 en bases SQLite preexistentes (por ejemplo migradas desde v1.0) donde EnsureCreatedAsync omite la creación de tablas nuevas.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        private static async Task ReconciliarEsquemaFase1Async(
            UmadDbContext db,
            CancellationToken cancellationToken)
        {
            await CrearTablaSiFaltaAsync(db, "Personas", """
                CREATE TABLE "Personas" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Personas" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "Matricula" TEXT NULL,
                    "NombreCompleto" TEXT NOT NULL,
                    "Correo" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "Rol" INTEGER NOT NULL,
                    "Activo" INTEGER NOT NULL DEFAULT 1,
                    "FechaCreacionUtc" TEXT NOT NULL,
                    "FechaActualizacionUtc" TEXT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await CrearTablaSiFaltaAsync(db, "TokensAcceso", """
                CREATE TABLE "TokensAcceso" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TokensAcceso" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "PersonaId" INTEGER NOT NULL,
                    "CodigoFirmado" TEXT NOT NULL,
                    "TipoPase" INTEGER NOT NULL,
                    "FechaEmision" TEXT NOT NULL,
                    "FechaExpiracion" TEXT NOT NULL,
                    "Revocado" INTEGER NOT NULL DEFAULT 0,
                    "FechaRevocacionUtc" TEXT NULL,
                    "MotivoRevocacion" TEXT NULL,
                    CONSTRAINT "FK_TokensAcceso_Personas_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT
                );
                """, cancellationToken).ConfigureAwait(false);

            await CrearTablaSiFaltaAsync(db, "RegistrosAcceso", """
                CREATE TABLE "RegistrosAcceso" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_RegistrosAcceso" PRIMARY KEY AUTOINCREMENT,
                    "IdentificadorGlobal" TEXT NOT NULL,
                    "PersonaId" INTEGER NOT NULL,
                    "TokenAccesoId" INTEGER NULL,
                    "PuntoAcceso" TEXT NOT NULL,
                    "TipoMovimiento" INTEGER NOT NULL,
                    "Resultado" INTEGER NOT NULL,
                    "MotivoResultado" TEXT NULL,
                    "FechaHora" TEXT NOT NULL,
                    "Sincronizado" INTEGER NOT NULL DEFAULT 0,
                    "EstadoSincronizacion" INTEGER NOT NULL DEFAULT 0,
                    "FechaUltimoIntento" TEXT NULL,
                    "Reintentos" INTEGER NOT NULL DEFAULT 0,
                    "UltimoError" TEXT NULL,
                    "RutaEvidenciaFoto" TEXT NULL,
                    "HashEvidenciaFoto" TEXT NULL,
                    "GuardiaPersonaId" INTEGER NULL,
                    "EntradaRelacionadaId" INTEGER NULL,
                    CONSTRAINT "FK_RegistrosAcceso_Personas_PersonaId" FOREIGN KEY ("PersonaId") REFERENCES "Personas" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_RegistrosAcceso_TokensAcceso_TokenAccesoId" FOREIGN KEY ("TokenAccesoId") REFERENCES "TokensAcceso" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_RegistrosAcceso_Personas_GuardiaPersonaId" FOREIGN KEY ("GuardiaPersonaId") REFERENCES "Personas" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_RegistrosAcceso_RegistrosAcceso_EntradaRelacionadaId" FOREIGN KEY ("EntradaRelacionadaId") REFERENCES "RegistrosAcceso" ("Id") ON DELETE SET NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            string[] indicesFase1 =
            [
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Personas_IdentificadorGlobal" ON "Personas" ("IdentificadorGlobal");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Personas_Matricula" ON "Personas" ("Matricula") WHERE "Matricula" IS NOT NULL;""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Personas_Correo" ON "Personas" ("Correo");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TokensAcceso_IdentificadorGlobal" ON "TokensAcceso" ("IdentificadorGlobal");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_TokensAcceso_CodigoFirmado" ON "TokensAcceso" ("CodigoFirmado");""",
                """CREATE INDEX IF NOT EXISTS "IX_TokensAcceso_Persona_Estado_Vigencia" ON "TokensAcceso" ("PersonaId", "Revocado", "FechaExpiracion");""",
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_RegistrosAcceso_IdentificadorGlobal" ON "RegistrosAcceso" ("IdentificadorGlobal");""",
                """CREATE INDEX IF NOT EXISTS "IX_RegistrosAcceso_FechaHora" ON "RegistrosAcceso" ("FechaHora");""",
                """CREATE INDEX IF NOT EXISTS "IX_RegistrosAcceso_Sincronizado" ON "RegistrosAcceso" ("Sincronizado");""",
                """CREATE INDEX IF NOT EXISTS "IX_RegistrosAcceso_EstadoSincronizacion" ON "RegistrosAcceso" ("EstadoSincronizacion");"""
            ];

            foreach (string indice in indicesFase1)
            {
                await db.Database.ExecuteSqlRawAsync(indice, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reconcilia las tablas y columnas de Fase 2 en bases creadas por Fase 1 mediante EnsureCreated, sin eliminar datos existentes.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
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
                    "VigenciaSolicitada" TEXT NULL,
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

        /// <summary>
        /// Agrega una columna a una tabla existente únicamente cuando no fue creada previamente por otra fase.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
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

        /// <summary>
        /// Crea una tabla de Fase 2 sin afectar tablas o datos preexistentes de Fase 1.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
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

        /// <summary>
        /// Comprueba la existencia de tablas o índices en SQLite consultando sqlite_master.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
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

        /// <summary>
        /// Comprueba si una columna específica existe dentro de una tabla SQLite.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
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

        /// <summary>
        /// Crea de manera idempotente los índices operativos de Fase 2 sin alterar registros existentes.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        private static async Task EjecutarIndicesFase2Async(
            UmadDbContext db,
            CancellationToken cancellationToken)
        {
            string[] indices =
            [
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_IdentificadorGlobal" ON "SolicitudesRenovacion" ("IdentificadorGlobal");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_PersonaId" ON "SolicitudesRenovacion" ("PersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_Estado" ON "SolicitudesRenovacion" ("Estado");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_TokenAccesoOriginalId" ON "SolicitudesRenovacion" ("TokenAccesoOriginalId");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_ResueltoPorPersonaId" ON "SolicitudesRenovacion" ("ResueltoPorPersonaId");""",
                """CREATE INDEX IF NOT EXISTS "IX_SolicitudesRenovacion_TokenAccesoNuevoId" ON "SolicitudesRenovacion" ("TokenAccesoNuevoId");""",
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
