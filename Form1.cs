using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class Form1 : Form
    {
        private TextBox txtSearch;
        private DataGridView dgvClients;
        private Button btnConfirm;
        private Button btnBack;
        private TextBox txtSelectedName;
        private TextBox txtSelectedPhone;

        private readonly HttpClient _httpClient;
        private int _selectedClientId;

        public Form1()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:8080/")
            };
            InitializeComponent();
            SetupSearchGrid();
        }

        private void InitializeComponent()
        {
            // Search textbox
            txtSearch = new TextBox
            {
                Location = new Point(12, 12),
                Width = 300,
                PlaceholderText = "Введите имя, ID или телефон"
            };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            // Selected name (readonly), hidden initially
            txtSelectedName = new TextBox
            {
                Location = txtSearch.Location,
                Width = 300,
                ReadOnly = true,
                Visible = false
            };

            // Selected phone (readonly), hidden initially
            txtSelectedPhone = new TextBox
            {
                Location = new Point(330, 12),
                Width = 150,
                ReadOnly = true,
                Visible = false
            };

            // DataGridView
            dgvClients = new DataGridView
            {
                Location = new Point(12, 40),
                Size = new Size(600, 300),
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };

            // Confirm button
            btnConfirm = new Button
            {
                Text = "Подтвердить",
                Location = new Point(12, 350),
                Size = new Size(120, 30),
                Enabled = false
            };
            btnConfirm.Click += async (_, __) => await OnConfirmAsync();

            // Back button
            btnBack = new Button
            {
                Text = "Назад",
                Location = btnConfirm.Location,
                Size = btnConfirm.Size,
                Visible = false
            };
            btnBack.Click += (_, __) => OnBack();

            // Wire up row selection to enable Confirm
            dgvClients.SelectionChanged += (_, __) =>
            {
                btnConfirm.Enabled = dgvClients.SelectedRows.Count > 0;
            };

            // Add all controls
            Controls.Add(txtSearch);
            Controls.Add(txtSelectedName);
            Controls.Add(txtSelectedPhone);
            Controls.Add(dgvClients);
            Controls.Add(btnConfirm);
            Controls.Add(btnBack);

            // Form settings
            Text = "Поиск клиентов / Исследования";
            ClientSize = new Size(640, 400);
        }

        private void SetupSearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNumber",
                HeaderText = "№",
                Width = 40,
                ReadOnly = true
            });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Полное имя",
                DataPropertyName = "FullName",
                Width = 250
            });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhone",
                HeaderText = "Телефон",
                DataPropertyName = "PhoneNumber",
                Width = 150
            });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                HeaderText = "ID",
                DataPropertyName = "Id",
                Visible = false
            });

            txtSearch.Visible = true;
            btnConfirm.Visible = true;
            txtSelectedName.Visible = false;
            txtSelectedPhone.Visible = false;
            btnBack.Visible = false;
        }

        private void SetupResearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNumber",
                HeaderText = "№",
                Width = 40,
                ReadOnly = true
            });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDate",
                HeaderText = "Дата исследования",
                Width = 200
            });

            txtSearch.Visible = false;
            btnConfirm.Visible = false;
            txtSelectedName.Visible = true;
            txtSelectedPhone.Visible = true;
            btnBack.Visible = true;
        }

        private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await SearchClientsAsync(txtSearch.Text.Trim());
            }
        }

        private async Task SearchClientsAsync(string query)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(query)
                    ? "api/clients"
                    : $"api/clients?q={Uri.EscapeDataString(query)}";
                var clients = await _httpClient
                    .GetFromJsonAsync<List<ClientDto>>(url)
                    ?? new List<ClientDto>();

                dgvClients.Rows.Clear();
                int rowNum = 1;
                foreach (var c in clients)
                {
                    dgvClients.Rows.Add(rowNum++, c.FullName, c.PhoneNumber, c.Id);
                }
                btnConfirm.Enabled = dgvClients.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке клиентов:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnConfirmAsync()
        {
            if (dgvClients.SelectedRows.Count == 0) return;
            var row = dgvClients.SelectedRows[0];
            _selectedClientId = Convert.ToInt32(row.Cells["colId"].Value);
            string name  = row.Cells["colName"].Value?.ToString() ?? "";
            string phone = row.Cells["colPhone"].Value?.ToString() ?? "";

            // Показываем данные выбранного пациента
            txtSelectedName.Text = name;
            txtSelectedPhone.Text = phone;

            // Переключаем грид в режим исследований
            SetupResearchGrid();

            // Загружаем исследования
            await SearchResearchesAsync(_selectedClientId);
        }

        private async Task SearchResearchesAsync(int clientId)
        {
            try
            {
                var researches = await _httpClient
                    .GetFromJsonAsync<List<ResearchDto>>(
                        $"api/clients/{clientId}/researches"
                    ) ?? new List<ResearchDto>();

                dgvClients.Rows.Clear();
                int rowNum = 1;
                foreach (var r in researches)
                {
                    dgvClients.Rows.Add(rowNum++, r.StudyDate.ToString("yyyy-MM-dd"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке исследований:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBack()
        {
            SetupSearchGrid();
            // по желанию, очистить поиск
            txtSearch.Text = "";
            dgvClients.Rows.Clear();
            btnConfirm.Enabled = false;
        }

        // DTO для клиентов
        private class ClientDto
        {
            public int Id { get; set; }
            public string FullName { get; set; } = "";
            public string PhoneNumber { get; set; } = "";
        }

        // DTO для исследований
        private class ResearchDto
        {
            public string OrthancId { get; set; } = "";
            public DateTime StudyDate { get; set; }
        }
    }
}
