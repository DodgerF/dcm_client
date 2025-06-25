using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Configuration;

namespace client
{
    public class LoginForm : Form
    {
        private readonly HttpClient _httpClient;

        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Button btnLogin = null!;

        private Label lblOtp = null!;
        private TextBox txtOtp = null!;
        private Button btnVerify = null!;

        public string JwtToken { get; private set; } = null!;
        public string UserRole { get; private set; } = null!;

        public LoginForm()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            string baseUrlString = ConfigurationManager.AppSettings["ServerBaseUrl"];
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrlString)
            };

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Вход в систему";
            ClientSize = new Size(350, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var lblUser = new Label
            {
                Text = "Email (логин):",
                Location = new Point(20, 20),
                AutoSize = true
            };
            txtUsername = new TextBox
            {
                Location = new Point(20, 45),
                Width = 300
            };

            var lblPass = new Label
            {
                Text = "Пароль:",
                Location = new Point(20, 75),
                AutoSize = true
            };
            txtPassword = new TextBox
            {
                Location = new Point(20, 100),
                Width = 300,
                UseSystemPasswordChar = true
            };

            btnLogin = new Button
            {
                Text = "Войти",
                Location = new Point(20, 135),
                Size = new Size(100, 30)
            };
            btnLogin.Click += async (s, e) => await DoLoginAsync();

            lblOtp = new Label
            {
                Text = "Введите 6-значный код:",
                Location = new Point(20, 20),
                AutoSize = true,
                Visible = false
            };
            txtOtp = new TextBox
            {
                Location = new Point(20, 45),
                Width = 150,
                MaxLength = 6,
                Visible = false
            };
            btnVerify = new Button
            {
                Text = "Подтвердить код",
                Location = new Point(20, 85),
                Size = new Size(120, 30),
                Visible = false
            };
            btnVerify.Click += async (s, e) => await DoVerifyAsync();

            Controls.AddRange(new Control[]
            {
                lblUser, txtUsername,
                lblPass, txtPassword,
                btnLogin,
                lblOtp, txtOtp, btnVerify
            });
        }

        private async Task DoLoginAsync()
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, введите логин и пароль.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnLogin.Enabled = false;
            try
            {
                var resp = await _httpClient.PostAsJsonAsync("api/auth/login", new { username, password });
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Неверное имя пользователя или пароль.", "Ошибка аутентификации", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnLogin.Enabled = true;
                    return;
                }

                SwitchToOtpMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при попытке входа: {ex.Message}", "Сетевая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnLogin.Enabled = true;
            }
        }

        private void SwitchToOtpMode()
        {
            txtUsername.Visible = false;
            txtPassword.Visible = false;
            btnLogin.Visible = false;

            foreach (Control c in Controls)
            {
                if (c is Label && c != lblOtp)
                {
                    c.Visible = false;
                }
            }

            lblOtp.Visible = true;
            txtOtp.Visible = true;
            btnVerify.Visible = true;
            txtOtp.Focus();
        }
        private async Task DoVerifyAsync()
        {
            string otp = txtOtp.Text.Trim();
            string username = txtUsername.Text.Trim();

            if (otp.Length != 6 || !int.TryParse(otp, out _))
            {
                MessageBox.Show("Пожалуйста, введите корректный 6-значный код.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnVerify.Enabled = false;
            try
            {
                var resp = await _httpClient.PostAsJsonAsync("api/auth/verify", new { username, code = otp });
                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Неверный или просроченный код.", "Ошибка верификации", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnVerify.Enabled = true;
                    return;
                }

                var authResp = await resp.Content.ReadFromJsonAsync<AuthResponseDto>();
                if (authResp == null || string.IsNullOrEmpty(authResp.token))
                {
                    MessageBox.Show("Не удалось получить токен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnVerify.Enabled = true;
                    return;
                }

                JwtToken = authResp.token;
                UserRole = ExtractRoleFromJwt(JwtToken);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке кода: {ex.Message}", "Сетевая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnVerify.Enabled = true;
            }
        }

        private string ExtractRoleFromJwt(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return "";

                string payload = parts[1];

                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var bytes = Convert.FromBase64String(payload);
                string json = Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("role", out var prop))
                {
                    return prop.GetString() ?? "";
                }
            }
            catch
            {
                
            }

            return "";
        }
    }
}
