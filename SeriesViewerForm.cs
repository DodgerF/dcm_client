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
    public class SeriesViewerForm : Form
    {
        private readonly HttpClient _httpClient;
        private readonly string     _studyId;
        private readonly string     _seriesId;
        
        private PictureBox picBox = null!;
        private TrackBar   trackBar = null!;
        private TextBox    txtFrameIndex = null!;
        private Label      lblTotalFrames = null!;
        private Label      lblMouseCoords = null!;
        
        private ComboBox   cmbPresets = null!;
        private TextBox    txtWindowWidth = null!;
        private TextBox    txtWindowLevel = null!;
        private Label      lblSlash = null!;

        // Словарь предустановок: название → (WW, WL)
        private readonly Dictionary<string, (int WW, int WL)> _windowPresets =
            new Dictionary<string, (int, int)>
            {
                { "Мягкие ткани",      (350,   40)   },
                { "Костные ткани",     (2000,  400)  },
                { "Лёгкие",            (1500, -600)  },
                { "Пользовательские",  (0,     0)    }
            };

        // Список всех instanceId-ов этой серии
        private List<string> _instanceIds = new List<string>();

        public SeriesViewerForm(HttpClient httpClient, string studyId, string seriesId, string seriesName)
        {
            _httpClient = httpClient;
            _studyId    = studyId;
            _seriesId   = seriesId;

            Text = $"Просмотр серии: {seriesName}";
            ClientSize = new Size(820, 720);
            MinimumSize = new Size(820, 720);

            InitializeControls();
            AttachEvents();

            // Асинхронно загружаем список инстансов
            _ = LoadInstanceListAsync();
        }

        private void InitializeControls()
        {
            // 1. PictureBox (для вывода PNG)
            picBox = new PictureBox
            {
                Location    = new Point(10, 10),
                Size        = new Size(780, 520),
                SizeMode    = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(picBox);

            // 2. TrackBar (для переключения между инстансами)
            trackBar = new TrackBar
            {
                Location   = new Point(10, 540),
                Size       = new Size(780, 45),
                Minimum    = 0,
                Maximum    = 0,    // пока нет данных
                TickStyle  = TickStyle.None,
                Enabled    = false
            };
            Controls.Add(trackBar);

            // 3. TextBox для ввода номера кадра
            txtFrameIndex = new TextBox
            {
                Location  = new Point(10, 595),
                Size      = new Size(50,  20),
                TextAlign = HorizontalAlignment.Right,
                Enabled   = false
            };
            Controls.Add(txtFrameIndex);

            // 4. Label «/ total»
            lblTotalFrames = new Label
            {
                Location  = new Point(65, 598),
                AutoSize  = true,
                Text      = "/ 0"
            };
            Controls.Add(lblTotalFrames);

            // 5. Label для отображения координат мыши в правом нижнем углу
            lblMouseCoords = new Label
            {
                Location  = new Point(690, 598),
                AutoSize  = true,
                Text      = "(X: 0, Y: 0)"
            };
            Controls.Add(lblMouseCoords);

            // --- Настройки окна контрастности вниз под «/ total» ---
            // 6. ComboBox для предустановок
            cmbPresets = new ComboBox
            {
                Location      = new Point(10, 630),
                Size          = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var key in _windowPresets.Keys)
            {
                cmbPresets.Items.Add(key);
            }
            cmbPresets.SelectedItem = "Мягкие ткани";
            Controls.Add(cmbPresets);

            // 7. TextBox для WW (изначально ReadOnly, так как выбран «Мягкие ткани»)
            txtWindowWidth = new TextBox
            {
                Location  = new Point(170, 630),
                Size      = new Size(60, 25),
                ReadOnly  = true,
                Text      = _windowPresets["Мягкие ткани"].WW.ToString()
            };
            Controls.Add(txtWindowWidth);

            // 8. Label «/»
            lblSlash = new Label
            {
                Location = new Point(235, 633),
                AutoSize = true,
                Text     = "/"
            };
            Controls.Add(lblSlash);

            // 9. TextBox для WL (изначально ReadOnly)
            txtWindowLevel = new TextBox
            {
                Location  = new Point(250, 630),
                Size      = new Size(60, 25),
                ReadOnly  = true,
                Text      = _windowPresets["Мягкие ткани"].WL.ToString()
            };
            Controls.Add(txtWindowLevel);
        }

        private void AttachEvents()
        {
            // Событие прокрутки TrackBar
            trackBar.Scroll += async (_, __) =>
            {
                int idx = trackBar.Value;
                txtFrameIndex.Text = (idx + 1).ToString();
                await LoadAndDisplayInstanceAsync(idx);
            };

            // Событие Enter в поле ввода номера кадра
            txtFrameIndex.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;

                    if (int.TryParse(txtFrameIndex.Text.Trim(), out int userIdx))
                    {
                        // Преобразуем 1-based в 0-based
                        int zeroBased = userIdx - 1;
                        if (zeroBased >= 0 && zeroBased < _instanceIds.Count)
                        {
                            trackBar.Value = zeroBased;
                            await LoadAndDisplayInstanceAsync(zeroBased);
                        }
                        else
                        {
                            MessageBox.Show("Неверный номер кадра.", "Ошибка",
                                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            };

            // Отслеживаем движение мыши по PictureBox
            picBox.MouseMove += (sender, e) =>
            {
                if (picBox.Image != null)
                {
                    lblMouseCoords.Text = $"(X: {e.X}, Y: {e.Y})";
                }
            };

            // Выбор пункта в ComboBox (предустановки)
            cmbPresets.SelectedIndexChanged += (sender, e) =>
            {
                if (cmbPresets.SelectedItem is not string selName) return;
                if (!_windowPresets.ContainsKey(selName)) return;

                if (selName != "Пользовательские")
                {
                    // Если выбрана НЕ «Пользовательские», то подставляем жестко, делаем поля ReadOnly
                    var (ww, wl) = _windowPresets[selName];
                    txtWindowWidth.Text = ww.ToString();
                    txtWindowLevel.Text = wl.ToString();

                    txtWindowWidth.ReadOnly = true;
                    txtWindowLevel.ReadOnly = true;
                }
                else
                {
                    // При выборе «Пользовательские» — разрешаем редактировать
                    txtWindowWidth.ReadOnly = false;
                    txtWindowLevel.ReadOnly = false;
                }
            };

            // Если пользователь вручную меняет WW или WL, переключаем на «Пользовательские» и делаем текстовые поля редактируемыми
            txtWindowWidth.TextChanged += (sender, e) => OnWindowTextChanged();
            txtWindowLevel.TextChanged += (sender, e) => OnWindowTextChanged();

            // Реагируем на изменение размера формы, чтобы PictureBox и остальные контролы растягивались
            this.Resize += (sender, e) =>
            {
                picBox.Size = new Size(ClientSize.Width - 40, ClientSize.Height - 260);
                trackBar.Size = new Size(ClientSize.Width - 40, trackBar.Height);
                trackBar.Location = new Point(10, picBox.Bottom + 10);

                txtFrameIndex.Location = new Point(10, trackBar.Bottom + 10);
                lblTotalFrames.Location = new Point(65, trackBar.Bottom + 13);
                lblMouseCoords.Location = new Point(ClientSize.Width - 130, trackBar.Bottom + 13);

                cmbPresets.Location = new Point(10, trackBar.Bottom + 45);
                txtWindowWidth.Location = new Point(170, trackBar.Bottom + 45);
                lblSlash.Location = new Point(235, trackBar.Bottom + 48);
                txtWindowLevel.Location = new Point(250, trackBar.Bottom + 45);

                lblTotalFrames.Text = $"/ {_instanceIds.Count}";
            };
        }

        /// <summary>
        /// Если поля WW/WL изменились вручную и не соответствуют ни одной предустановке,
        /// то переключаем ComboBox на «Пользовательские» и делаем поля editable.
        /// </summary>
        private void OnWindowTextChanged()
        {
            bool parsedWw = int.TryParse(txtWindowWidth.Text.Trim(), out int currentWw);
            bool parsedWl = int.TryParse(txtWindowLevel.Text.Trim(), out int currentWl);

            if (!parsedWw || !parsedWl)
            {
                SetPresetSilently("Пользовательские");
                txtWindowWidth.ReadOnly = false;
                txtWindowLevel.ReadOnly = false;
                return;
            }

            // Ищём, совпадает ли пара (WW,WL) с одной из заводских пресетов (кроме «Пользовательские»)
            foreach (var kvp in _windowPresets)
            {
                string presetName = kvp.Key;
                var (presetWw, presetWl) = kvp.Value;

                if (presetName == "Пользовательские") continue;
                if (currentWw == presetWw && currentWl == presetWl)
                {
                    SetPresetSilently(presetName);
                    txtWindowWidth.ReadOnly = true;
                    txtWindowLevel.ReadOnly = true;
                    return;
                }
            }

            // Если не нашли точного совпадения, переключаем на «Пользовательские»
            SetPresetSilently("Пользовательские");
            txtWindowWidth.ReadOnly = false;
            txtWindowLevel.ReadOnly = false;
        }

        /// <summary>
        /// Переключает ComboBox на нужный пункт **без** вызова внешнего обработчика SelectedIndexChanged дважды.
        /// </summary>
        private void SetPresetSilently(string presetName)
        {
            if (cmbPresets.SelectedItem as string == presetName) return;

            cmbPresets.SelectedIndexChanged -= CmbPresets_SelectedIndexChanged;
            cmbPresets.SelectedItem = presetName;
            cmbPresets.SelectedIndexChanged += CmbPresets_SelectedIndexChanged;
        }

        private void CmbPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbPresets.SelectedItem is not string selName) return;
            if (!_windowPresets.ContainsKey(selName)) return;

            if (selName != "Пользовательские")
            {
                var (ww, wl) = _windowPresets[selName];
                txtWindowWidth.Text = ww.ToString();
                txtWindowLevel.Text = wl.ToString();

                txtWindowWidth.ReadOnly = true;
                txtWindowLevel.ReadOnly = true;
            }
            else
            {
                txtWindowWidth.ReadOnly = false;
                txtWindowLevel.ReadOnly = false;
            }
        }

        /// <summary>
        /// Асинхронно запрашивает у сервера список всех instanceId-ов для затронутой серии,
        /// затем настраивает TrackBar и показывает первый кадр.
        /// </summary>
        private async Task LoadInstanceListAsync()
        {
            try
            {
                _instanceIds = await _httpClient
                    .GetFromJsonAsync<List<string>>(
                        $"api/studies/{_studyId}/series/{_seriesId}/instances"
                    ) ?? new List<string>();

                if (_instanceIds.Count > 0)
                {
                    trackBar.Maximum = _instanceIds.Count - 1;
                    trackBar.Enabled = true;
                    txtFrameIndex.Enabled = true;
                    lblTotalFrames.Text = $"/ {_instanceIds.Count}";

                    // Показать первый кадр (индекс 0)
                    trackBar.Value = 0;
                    txtFrameIndex.Text = "1";
                    await LoadAndDisplayInstanceAsync(0);
                }
                else
                {
                    MessageBox.Show("В серии не найдено ни одного инстанса.",
                                    "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка инстансов:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Загружает и отображает конкретный экземпляр (instance) по его индексу в списке.
        /// При этом учитывает текущие значения WW и WL.
        /// </summary>
        private async Task LoadAndDisplayInstanceAsync(int index)
        {
            if (index < 0 || index >= _instanceIds.Count) return;
            string instId = _instanceIds[index];

            try
            {
                // Формируем URL с параметрами ww и wl, если они корректны
                string url = $"api/studies/{_studyId}/series/{_seriesId}/instances/{instId}/preview";
                bool hasWw = int.TryParse(txtWindowWidth.Text.Trim(), out int ww);
                bool hasWl = int.TryParse(txtWindowLevel.Text.Trim(), out int wl);

                if (hasWw && hasWl)
                {
                    url += $"?ww={ww}&wl={wl}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();
                using var ms = new System.IO.MemoryStream(pngBytes);
                Image img = Image.FromStream(ms);

                // Отображаем картинку
                picBox.Image = img;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить превью кадра:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
