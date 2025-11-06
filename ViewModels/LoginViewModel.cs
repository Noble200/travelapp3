using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Allva.Desktop.Services;

namespace Allva.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel para la pantalla de inicio de sesión
    /// Implementa toda la lógica de autenticación y validación
    /// ACTUALIZADO: Diferencia entre Administradores del Sistema y Usuarios Normales
    /// </summary>
    public partial class LoginViewModel : ObservableObject
    {
        // Nota: Descomentar estas líneas cuando implementes los servicios
        // private readonly IAuthenticationService _authService;
        // private readonly INavigationService _navigationService;
        // private readonly IDialogService _dialogService;

        // ============================================
        // LOCALIZACIÓN
        // ============================================

        public LocalizationService Localization => LocalizationService.Instance;

        // ============================================
        // PROPIEDADES OBSERVABLES
        // ============================================

        [ObservableProperty]
        private string _numeroUsuario = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _codigoLocal = string.Empty;

        [ObservableProperty]
        private bool _recordarSesion = false;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _mensajeError = string.Empty;

        [ObservableProperty]
        private bool _mostrarError = false;

        [ObservableProperty]
        private int _intentosFallidos = 0;

        [ObservableProperty]
        private bool _usuarioBloqueado = false;

        [ObservableProperty]
        private int _tiempoBloqueoRestante = 0;

        // ============================================
        // CONSTRUCTOR
        // ============================================

        public LoginViewModel()
        {
            // Cuando implementes los servicios, usa este constructor:
            // _authService = authService;
            // _navigationService = navigationService;
            // _dialogService = dialogService;

            // Cargar datos guardados si existe
            CargarDatosGuardados();
        }

        // Constructor con inyección de dependencias (descomentar cuando tengas los servicios)
        /*
        public LoginViewModel(
            IAuthenticationService authService,
            INavigationService navigationService,
            IDialogService dialogService)
        {
            _authService = authService;
            _navigationService = navigationService;
            _dialogService = dialogService;

            CargarDatosGuardados();
        }
        */

        // ============================================
        // COMANDOS
        // ============================================

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            try
            {
                IsLoading = true;
                MostrarError = false;
                MensajeError = string.Empty;

                // Validar campos
                if (!ValidarCampos())
                {
                    return;
                }

                // Preparar request (sin UUID/MAC/IP que causan problemas)
                var loginRequest = new LoginRequest
                {
                    NumeroUsuario = NumeroUsuario.Trim(),
                    Password = Password,
                    CodigoLocal = CodigoLocal.Trim().ToUpper(),
                    UUID = "DEV-DEVICE", // Temporal para desarrollo
                    MAC = "00:00:00:00:00:00", // Temporal para desarrollo
                    IP = "127.0.0.1", // Temporal para desarrollo
                    UserAgent = "AllvaDesktop/1.0"
                };

                // TODO: Llamar al servicio de autenticación cuando lo implementes
                // var response = await _authService.LoginAsync(loginRequest);

                // ============================================
                // SIMULACIÓN DE LOGIN PARA TESTING
                // ============================================
                await Task.Delay(1000); // Simular delay de red
                
                // ⭐ NUEVA LÓGICA: Verificar si es Administrador del Sistema (usuarios 0001, 0002)
                bool esAdministradorSistema = (NumeroUsuario == "0001" || NumeroUsuario == "0002") && 
                                              Password == "Admin123!";
                
                // Verificar usuarios normales (9999/1001/1002)
                bool esUsuarioNormal = (NumeroUsuario == "9999" && Password == "Test1234!") ||
                                       (NumeroUsuario == "1001" && Password == "Admin123!") ||
                                       (NumeroUsuario == "1002" && Password == "Usuario123!");

                bool loginExitoso = esAdministradorSistema || esUsuarioNormal;

                if (loginExitoso)
                {
                    // Resetear intentos fallidos
                    IntentosFallidos = 0;
                    UsuarioBloqueado = false;

                    // Guardar datos si se seleccionó "recordar"
                    if (RecordarSesion)
                    {
                        GuardarDatosLocales();
                    }
                    else
                    {
                        LimpiarDatosGuardados();
                    }

                    // ⭐ PREPARAR DATOS DE SESIÓN SEGÚN TIPO DE USUARIO
                    var loginData = new LoginSuccessData
                    {
                        UserName = GetUserNameFromNumber(NumeroUsuario),
                        UserNumber = NumeroUsuario,
                        LocalCode = CodigoLocal.ToUpper(),
                        Token = $"token-{Guid.NewGuid()}"
                    };

                    // ⭐ DETERMINAR TIPO DE USUARIO Y ROL
                    if (esAdministradorSistema)
                    {
                        // Usuario Administrador del Sistema
                        loginData.UserType = "ADMIN_SISTEMA";
                        loginData.RoleName = "Administrador_Sistema";
                        loginData.IsSystemAdmin = true;
                    }
                    else
                    {
                        // Usuario Normal (Administrador, Gerente o Empleado de local)
                        loginData.UserType = DeterminarTipoUsuario(NumeroUsuario);
                        loginData.RoleName = DeterminarRol(NumeroUsuario);
                        loginData.IsSystemAdmin = false;
                    }

                    // ⭐ NAVEGAR AL DASHBOARD CORRESPONDIENTE
                    var navigationService = new NavigationService();
                    
                    if (loginData.IsSystemAdmin)
                    {
                        // Redirigir al Panel de Administración del Sistema
                        navigationService.NavigateToAdminDashboard(loginData);
                    }
                    else
                    {
                        // Redirigir al Dashboard Normal de Usuario
                        navigationService.NavigateToDashboard(loginData);
                    }
                }
                else
                {
                    // Login fallido
                    ManejarLoginFallido("Credenciales incorrectas", "PASSWORD_INCORRECTO");
                }
            }
            catch (Exception ex)
            {
                // Mostrar el error real en lugar de mensaje genérico
                MensajeError = $"Error al iniciar sesión: {ex.Message}";
                MostrarError = true;
                
                // Log del error para debugging
                Console.WriteLine($"Error en login: {ex}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RecuperarPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(NumeroUsuario))
            {
                // TODO: Usar DialogService cuando lo implementes
                // await _dialogService.ShowWarningAsync(...)
                
                MostrarMensajeError(Localization["Recovery_UserRequiredMessage"]);
                return;
            }

            try
            {
                IsLoading = true;
                
                // TODO: Implementar cuando tengas el servicio
                // var resultado = await _authService.SolicitarRecuperacionPasswordAsync(NumeroUsuario);
                
                // Simulación
                await Task.Delay(1000);
                MostrarMensajeError(Localization["Recovery_SuccessMessage"]);
            }
            catch (Exception ex)
            {
                MostrarMensajeError(Localization["Recovery_ErrorGeneric"]);
                Console.WriteLine($"Error en recuperación: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void LimpiarFormulario()
        {
            NumeroUsuario = string.Empty;
            Password = string.Empty;
            CodigoLocal = string.Empty;
            MostrarError = false;
            MensajeError = string.Empty;
        }

        [RelayCommand]
        private void CambiarIdioma()
        {
            Localization.ToggleLanguage();
            
            // Si hay un mensaje de error, actualizarlo al nuevo idioma
            if (MostrarError && !string.IsNullOrEmpty(MensajeError))
            {
                // Recargar el mensaje de error en el nuevo idioma
                MostrarError = false;
                MensajeError = string.Empty;
            }
        }

        // ============================================
        // MÉTODOS DE VALIDACIÓN
        // ============================================

        private bool CanLogin()
        {
            return !IsLoading && 
                   !UsuarioBloqueado &&
                   !string.IsNullOrWhiteSpace(NumeroUsuario) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !string.IsNullOrWhiteSpace(CodigoLocal);
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(NumeroUsuario))
            {
                MostrarMensajeError(Localization["Error_UserRequired"]);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                MostrarMensajeError(Localization["Error_PasswordRequired"]);
                return false;
            }

            if (Password.Length < 8)
            {
                MostrarMensajeError(Localization["Error_PasswordMinLength"]);
                return false;
            }

            if (string.IsNullOrWhiteSpace(CodigoLocal))
            {
                MostrarMensajeError(Localization["Error_OfficeRequired"]);
                return false;
            }

            return true;
        }

        private void ManejarLoginFallido(string mensaje, string tipoError)
        {
            IntentosFallidos++;
            
            if (IntentosFallidos >= 5)
            {
                UsuarioBloqueado = true;
                TiempoBloqueoRestante = 15; // Minutos
                MostrarMensajeError(string.Format(Localization["Error_UserBlocked"], TiempoBloqueoRestante));
            }
            else
            {
                int intentosRestantes = 5 - IntentosFallidos;
                MostrarMensajeError(string.Format(Localization["Error_PasswordIncorrect"], intentosRestantes));
            }
        }

        private void MostrarMensajeError(string mensaje)
        {
            MensajeError = mensaje;
            MostrarError = true;
        }

        // ============================================
        // MÉTODOS DE PERSISTENCIA LOCAL
        // ============================================

        private void CargarDatosGuardados()
        {
            try
            {
                // TODO: Implementar carga desde configuración local
                // Por ahora, valores por defecto
                NumeroUsuario = string.Empty;
                CodigoLocal = string.Empty;
                RecordarSesion = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar datos guardados: {ex.Message}");
            }
        }

        private void GuardarDatosLocales()
        {
            try
            {
                // TODO: Implementar guardado en configuración local
                // Guardar NumeroUsuario, CodigoLocal (NO password)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar datos: {ex.Message}");
            }
        }

        private void LimpiarDatosGuardados()
        {
            try
            {
                // TODO: Implementar limpieza de configuración local
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al limpiar datos: {ex.Message}");
            }
        }

        // ============================================
        // MÉTODOS AUXILIARES - DETERMINACIÓN DE ROLES
        // ============================================

        /// <summary>
        /// Determina el tipo de usuario basado en el número
        /// </summary>
        private string DeterminarTipoUsuario(string numeroUsuario)
        {
            return numeroUsuario switch
            {
                "1001" => "ADMIN",
                "1002" => "EMPLEADO",
                "1003" => "EMPLEADO",
                "9999" => "EMPLEADO",
                _ => "EMPLEADO"
            };
        }

        /// <summary>
        /// Determina el rol del usuario basado en el número
        /// </summary>
        private string DeterminarRol(string numeroUsuario)
        {
            return numeroUsuario switch
            {
                "1001" => "Administrador",
                "1002" => "Empleado",
                "1003" => "Empleado",
                "9999" => "Empleado",
                _ => "Empleado"
            };
        }

        private string GetUserNameFromNumber(string numeroUsuario)
        {
            // Mapeo simple de números a nombres para la demo
            return numeroUsuario switch
            {
                "0001" => "Admin Principal",
                "0002" => "Admin Secundario",
                "1001" => "Juan Pérez",
                "1002" => "María González",
                "1003" => "Carlos Rodríguez",
                "1004" => "Ana Martínez",
                "9999" => "Test User",
                _ => $"Usuario {numeroUsuario}"
            };
        }

        // ============================================
        // MÉTODOS AUXILIARES
        // ============================================

        partial void OnNumeroUsuarioChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnPasswordChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        partial void OnCodigoLocalChanged(string value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }
    }

    // ============================================
    // MODELOS AUXILIARES
    // ============================================

    /// <summary>
    /// Modelo para la solicitud de login
    /// </summary>
    public class LoginRequest
    {
        public string NumeroUsuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CodigoLocal { get; set; } = string.Empty;
        public string UUID { get; set; } = string.Empty;
        public string MAC { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}