using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class AddUserForm : Form
    {
        private TextBox txtFullName = null!;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private ComboBox cmbRole = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        private readonly HttpClient _httpClient;

        public AddUserForm(HttpClient httpClient)
        {
            _httpClient = httpClient;

            Text = "Добавить нового пользователя";
            ClientSize = new Size(400, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            InitializeComponent();
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

            var lblUsername = new Label
            {
                Text = "Почта:",
                Location = new Point(20, 60),
                AutoSize = true
            };
            txtUsername = new TextBox
            {
                Location = new Point(100, 58),
                Width = 260
            };

            var lblPassword = new Label
            {
                Text = "Пароль:",
                Location = new Point(20, 100),
                AutoSize = true
            };
            txtPassword = new TextBox
            {
                Location = new Point(100, 98),
                Width = 260,
                UseSystemPasswordChar = true
            };

            var lblRole = new Label
            {
                Text = "Роль:",
                Location = new Point(20, 140),
                AutoSize = true
            };
            cmbRole = new ComboBox
            {
                Location = new Point(100, 138),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            cmbRole.Items.AddRange(new string[] { "Врач", "Регистратор", "Администратор" });
            cmbRole.SelectedIndex = 0;

            btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(100, 200),
                Size = new Size(100, 30)
            };
            btnSave.Click += async (s, e) => await DoSaveAsync();

            btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(220, 200),
                Size = new Size(100, 30)
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[]
            {
                lblFullName, txtFullName,
                lblUsername, txtUsername,
                lblPassword, txtPassword,
                lblRole, cmbRole,
                btnSave, btnCancel
            });
        }

        private async Task DoSaveAsync()
        {
            string fullName = txtFullName.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;
            string roleName = cmbRole.SelectedItem.ToString() ?? "Регистратор";

            if (string.IsNullOrEmpty(fullName) ||
                string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSave.Enabled = false;

            var payload = new
            {
                fullName = fullName,
                username = username,
                password = password,
                roleName = roleName
            };

            try
            {
                var resp = await _httpClient.PostAsJsonAsync("api/admin/employees", payload);
                if (resp.IsSuccessStatusCode)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Ошибка при создании пользователя:\n{resp.StatusCode} - {text}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сетевая ошибка при создании пользователя:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
            }
        }
    }
}
