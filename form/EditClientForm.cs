using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class EditClientForm : Form
    {
        private readonly HttpClient _httpClient;
        private readonly int _clientId;

        private TextBox txtFullName = null!;
        private TextBox txtMedPolicy = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        public EditClientForm(HttpClient httpClient, int clientId)
        {
            _httpClient = httpClient;
            _clientId = clientId;

            Text = "Редактировать пациента";
            ClientSize = new Size(400, 220);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            InitializeComponent();
            Load += async (_, __) => await LoadClientDataAsync();
        }

        private void InitializeComponent()
        {
            var lblFullName = new Label
            {
                Text = "ФИО:",
                Location = new Point(20, 20),
                AutoSize = true
            };
            txtFullName = new TextBox
            {
                Location = new Point(100, 18),
                Width = 260
            };

            var lblMedPolicy = new Label
            {
                Text = "СНИЛС:",
                Location = new Point(20, 60),
                AutoSize = true
            };
            txtMedPolicy = new TextBox
            {
                Location = new Point(100, 58),
                Width = 260
            };

            btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(100, 120),
                Size = new Size(100, 30)
            };
            btnSave.Click += async (_, __) => await DoSaveAsync();

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(220, 120),
                Size = new Size(100, 30)
            };
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[]
            {
                lblFullName, txtFullName,
                lblMedPolicy, txtMedPolicy,
                btnSave, btnCancel
            });
        }

        private async Task LoadClientDataAsync()
        {
            try
            {
                var client = await _httpClient.GetFromJsonAsync<ClientDto>($"api/clients/{_clientId}");
                if (client == null)
                {
                    MessageBox.Show("Не удалось загрузить данные пациента.", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Cancel;
                    return;
                }

                txtFullName.Text = client.FullName;
                txtMedPolicy.Text = client.MedPolicy;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Cancel;
            }
        }

        private async Task DoSaveAsync()
        {
            string fullName = txtFullName.Text.Trim();
            string medPolicy = txtMedPolicy.Text.Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(medPolicy))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSave.Enabled = false;

            var payload = new
            {
                fullName = fullName,
                medPolicy = medPolicy
            };

            try
            {
                var resp = await _httpClient.PutAsJsonAsync(
                    $"api/clients/{_clientId}", payload);

                if (resp.IsSuccessStatusCode)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Ошибка при сохранении данных:\n{resp.StatusCode} - {text}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сетевая ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
            }
        }
    }
}
