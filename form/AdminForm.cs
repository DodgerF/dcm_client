using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class AdminForm : Form
    {
        private readonly HttpClient _httpClient;
        private readonly DataGridView dgvEmployees;
        private readonly Button btnAddUser;
        private readonly Button btnDeleteUser;
        private readonly TextBox txtSearchName;
        private readonly Label lblSearch;

        private List<EmployeeDto> _allEmployees = new List<EmployeeDto>();

        public AdminForm(string jwtToken)
        {
            Text = "Панель администратора";
            ClientSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:8444/")
            };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

            lblSearch = new Label
            {
                Text = "Поиск по ФИО:",
                Location = new Point(10, 15),
                AutoSize = true
            };
            txtSearchName = new TextBox
            {
                Location = new Point(100, 12),
                Width = 250
            };
            txtSearchName.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    await FilterGridAsync();
                }
            };

            btnAddUser = new Button
            {
                Text = "Добавить пользователя",
                Location = new Point(370, 10),
                Size = new Size(150, 30)
            };
            btnAddUser.Click += async (s, e) =>
            {
                using var dlg = new AddUserForm(_httpClient);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadEmployeesAsync();
                }
            };

            btnDeleteUser = new Button
            {
                Text = "Удалить пользователя",
                Location = new Point(540, 10),
                Size = new Size(150, 30),
                Enabled = false
            };
            btnDeleteUser.Click += async (s, e) => await DeleteSelectedEmployeeAsync();

            dgvEmployees = new DataGridView
            {
                Location = new Point(10,  50),
                Size = new Size(680, 430),
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };
            dgvEmployees.SelectionChanged += (_, __) =>
            {
                btnDeleteUser.Enabled = dgvEmployees.SelectedRows.Count > 0;
            };

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNumber",
                HeaderText = "№",
                Width = 50
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFullName",
                HeaderText = "ФИО",
                DataPropertyName = "FullName",
                Width = 250
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUsername",
                HeaderText = "Почта",
                DataPropertyName = "Username",
                Width = 200
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRole",
                HeaderText = "Роль",
                DataPropertyName = "RoleName",
                Width = 150
            });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                HeaderText = "ID",
                DataPropertyName = "Id",
                Visible = false
            });

            Controls.AddRange(new Control[]
            {
                lblSearch, txtSearchName,
                btnAddUser, btnDeleteUser,
                dgvEmployees
            });

            Load += async (s, e) => await LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                _allEmployees = await _httpClient
                    .GetFromJsonAsync<List<EmployeeDto>>("api/admin/employees")
                    ?? new List<EmployeeDto>();

                PopulateGrid(_allEmployees);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при загрузке списка сотрудников:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async Task FilterGridAsync()
        {
            string filter = txtSearchName.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                PopulateGrid(_allEmployees);
            }
            else
            {
                var filtered = _allEmployees
                    .Where(emp => emp.FullName.ToLower().Contains(filter))
                    .ToList();
                PopulateGrid(filtered);
            }
            await Task.CompletedTask;
        }


        private void PopulateGrid(List<EmployeeDto> list)
        {
            dgvEmployees.Rows.Clear();
            int idx = 1;
            foreach (var emp in list)
            {
                string displayRole = emp.RoleName switch
                {
                    "ROLE_ADMIN"       => "Администратор",
                    "ROLE_DOCTOR"      => "Врач",
                    "ROLE_REGISTRATOR" => "Регистратор",
                    _                  => emp.RoleName
                };

                dgvEmployees.Rows.Add(
                    idx++,
                    emp.FullName,
                    emp.Username,
                    displayRole,
                    emp.Id
                );
            }
            btnDeleteUser.Enabled = false;
        }

        private async Task DeleteSelectedEmployeeAsync()
        {
            if (dgvEmployees.SelectedRows.Count == 0)
                return;

            var row = dgvEmployees.SelectedRows[0];
            int id = Convert.ToInt32(row.Cells["colId"].Value);
            string fullName = row.Cells["colFullName"].Value?.ToString() ?? "";

            var confirm = MessageBox.Show(
                $"Вы действительно хотите удалить сотрудника \"{fullName}\"?",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                var resp = await _httpClient.DeleteAsync($"api/admin/employees/{id}");
                if (resp.IsSuccessStatusCode)
                {
                    await LoadEmployeesAsync();
                }
                else
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Не удалось удалить сотрудника:\n{resp.StatusCode} - {text}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Сетевая ошибка при удалении сотрудника:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
