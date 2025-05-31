using System;
using System.Collections.Generic;
using System.Drawing;
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

        // Этот Label теперь будет дочерним элементом picBox и рисоваться поверх картинки
        private Label      lblMouseCoords = null!;

        private ComboBox   cmbPresets = null!;
        private Label      lblWW = null!;      // «Window Width:»
        private TextBox    txtWindowWidth = null!;
        private Label      lblSlash = null!;   // «/»
        private Label      lblWL = null!;      // «Window Level:»
        private TextBox    txtWindowLevel = null!;

        // Для зума: оригинальное изображение и текущий масштаб
        private Bitmap?    _originalImage;
        private Bitmap?    _currentScaledImage;
        private float      _zoomFactor = 1.0f;
        private const float ZoomStep = 0.1f;   // 10% за один щелчок
        private const float MinZoom = 0.1f;    // 10%
        private const float MaxZoom = 10.0f;   // 1000%

        // Предустановки WW/WL
        private readonly Dictionary<string, (int WW, int WL)> _windowPresets =
            new Dictionary<string, (int, int)>
            {
                { "Мягкие ткани",      (350,   40)   },
                { "Костные ткани",     (2000,  400)  },
                { "Лёгкие",            (1500, -600)  },
                { "Пользовательские",  (0,     0)    }
            };

        // Список instanceId-ов
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

            // Начинаем загрузку списка инстансов
            _ = LoadInstanceListAsync();
        }

        private void InitializeControls()
        {
            //
            // 1. PictureBox: фиксированный размер 780×520, расположен в (10,10).
            //    SizeMode не используется, мы отрисовываем картинку вручную в Paint.
            //
            picBox = new PictureBox
            {
                Location    = new Point(10, 10),
                Size        = new Size(780, 520),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.Black,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left
            };
            // Подписка на MouseWheel и Paint
            picBox.MouseWheel += PicBox_MouseWheel;
            picBox.Paint      += PicBox_Paint;

            // Помещаем lblMouseCoords как дочерний элемент picBox
            lblMouseCoords = new Label
            {
                AutoSize   = true,
                BackColor  = Color.FromArgb(160, Color.Black),
                ForeColor  = Color.White,
                Text       = "(X: -, Y: -)",
                Anchor     = AnchorStyles.Bottom | AnchorStyles.Right
            };
            lblMouseCoords.Location = new Point(
                picBox.ClientSize.Width - lblMouseCoords.Width - 20,
                picBox.ClientSize.Height - lblMouseCoords.Height - 5
            );
            picBox.Controls.Add(lblMouseCoords);

            // Добавляем label внутрь picBox
            picBox.Controls.Add(lblMouseCoords);
            // Сформируем начальную позицию (будет скорректирована при ресайзе picBox)
            lblMouseCoords.Location = new Point(
                picBox.ClientSize.Width - lblMouseCoords.Width - 5,
                picBox.ClientSize.Height - lblMouseCoords.Height - 5
            );

            Controls.Add(picBox);

            //
            // 2. TrackBar (переключение между инстансами) под PictureBox
            //
            trackBar = new TrackBar
            {
                Location   = new Point(10, picBox.Bottom + 10),
                Size       = new Size(780, 45),
                Minimum    = 0,
                Maximum    = 0,    // Пока пусто
                TickStyle  = TickStyle.None,
                Enabled    = false,
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(trackBar);

            //
            // 3. TextBox для номера кадра
            //
            txtFrameIndex = new TextBox
            {
                Location  = new Point(10, trackBar.Bottom + 10),
                Size      = new Size(50,  20),
                TextAlign = HorizontalAlignment.Right,
                Enabled   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtFrameIndex);

            //
            // 4. Label «/ total»
            //
            lblTotalFrames = new Label
            {
                Location  = new Point(65, trackBar.Bottom + 13),
                AutoSize  = true,
                Text      = "/ 0",
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblTotalFrames);

            //
            // --- Блок предустановок + WW/WL под номером кадра ---
            //
            // 5. ComboBox с предустановками
            //
            cmbPresets = new ComboBox
            {
                Location      = new Point(10, txtFrameIndex.Bottom + 15),
                Size          = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor        = AnchorStyles.Top | AnchorStyles.Left
            };
            foreach (var key in _windowPresets.Keys)
            {
                cmbPresets.Items.Add(key);
            }
            cmbPresets.SelectedItem = "Мягкие ткани";
            Controls.Add(cmbPresets);

            //
            // 6. Label «Window Width:»
            //
            lblWW = new Label
            {
                Location = new Point(170, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "Window Width:",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblWW);

            //
            // 7. TextBox для WW
            //
            txtWindowWidth = new TextBox
            {
                Location = new Point(lblWW.Right + 5, txtFrameIndex.Bottom + 15),
                Size     = new Size(60, 25),
                ReadOnly = true,
                Text     = _windowPresets["Мягкие ткани"].WW.ToString(),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtWindowWidth);

            //
            // 8. Label «/»
            //
            lblSlash = new Label
            {
                Location = new Point(txtWindowWidth.Right + 5, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "/",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblSlash);

            //
            // 9. Label «Window Level:»
            //
            lblWL = new Label
            {
                Location = new Point(lblSlash.Right + 10, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "Window Level:",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblWL);

            //
            // 10. TextBox для WL
            //
            txtWindowLevel = new TextBox
            {
                Location = new Point(lblWL.Right + 5, txtFrameIndex.Bottom + 15),
                Size     = new Size(60, 25),
                ReadOnly = true,
                Text     = _windowPresets["Мягкие ткани"].WL.ToString(),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtWindowLevel);
        }

        private void AttachEvents()
        {
            //
            // Перемещение TrackBar: загружаем соответствующий кадр
            //
            trackBar.Scroll += async (_, __) =>
            {
                int idx = trackBar.Value;
                txtFrameIndex.Text = (idx + 1).ToString();
                await LoadAndDisplayInstanceAsync(idx);
            };

            //
            // Нажатие Enter в txtFrameIndex
            //
            txtFrameIndex.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled         = true;
                    e.SuppressKeyPress = true;

                    if (int.TryParse(txtFrameIndex.Text.Trim(), out int userIdx))
                    {
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

            //
            // Отслеживание мыши внутри picBox: обновляем координаты
            //
            picBox.MouseMove += (sender, e) =>
            {
                if (_currentScaledImage == null || _originalImage == null)
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                    return;
                }

                // Размеры текущего масштаба
                int sW = _currentScaledImage.Width;
                int sH = _currentScaledImage.Height;

                // Вычисляем смещение, чтобы картинка была центрирована внутри picBox
                int dx = (picBox.ClientSize.Width  - sW) / 2;
                int dy = (picBox.ClientSize.Height - sH) / 2;

                int mouseX = e.X;
                int mouseY = e.Y;

                // Если курсор внутри области реальной картинки
                if (mouseX >= dx && mouseX < dx + sW &&
                    mouseY >= dy && mouseY < dy + sH)
                {
                    // Пересчитаем координаты внутри оригинала
                    float imgX = (mouseX - dx) / _zoomFactor;
                    float imgY = (mouseY - dy) / _zoomFactor;

                    // Обрежем по границам
                    int ix = Math.Min(Math.Max((int)imgX, 0), _originalImage.Width - 1);
                    int iy = Math.Min(Math.Max((int)imgY, 0), _originalImage.Height - 1);

                    lblMouseCoords.Text = $"(X: {ix}, Y: {iy})";
                }
                else
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                }
            };

            //
            // Обработчик колесика мыши в picBox: зум
            //
            picBox.MouseWheel += PicBox_MouseWheel;

            //
            // Событие Paint: отрисовываем масштабированное изображение
            //
            picBox.Paint += PicBox_Paint;

            //
            // Смена предустановки WW/WL
            //
            cmbPresets.SelectedIndexChanged += (sender, e) =>
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
            };

            //
            // Если вручную изменили цифры в WW или WL — переключаемся на «Пользовательские»
            //
            txtWindowWidth.TextChanged += (sender, e) => OnWindowTextChanged();
            txtWindowLevel.TextChanged += (sender, e) => OnWindowTextChanged();

            //
            // Когда форма ресайзится — надо поправить позицию lblMouseCoords внутри picBox
            //
            picBox.Resize += (sender, e) =>
            {
                lblMouseCoords.Location = new Point(
                    picBox.ClientSize.Width - lblMouseCoords.Width - 20,
                    picBox.ClientSize.Height - lblMouseCoords.Height - 5
                );
                RecalculateCurrentScaledImage();
            };

            //
            // Если пользователь меняет размер главной формы — остальные контролы смещаются автоматически благодаря Anchor
            //
            this.Resize += (sender, e) =>
            {
                // При ресайзе формы trackBar и все, что ниже, автоматически смещаются по Anchor.
                // Но чтобы picBox остался на месте, мы не трогаем его — только пересчитаем центральное положение картинки.
                RecalculateCurrentScaledImage();
            };
        }

        /// <summary>
        /// Если WW/WL не совпадают с ни одной предустановкой, ставим «Пользовательские».
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

            SetPresetSilently("Пользовательские");
            txtWindowWidth.ReadOnly = false;
            txtWindowLevel.ReadOnly = false;
        }

        /// <summary>
        /// Сменить выбранный пункт ComboBox без повторного вызова обработчика.
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
        /// Запрашивает список instanceId-ов, настраивает TrackBar и показывает первый кадр.
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

                    // Показать первый кадр
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
        /// Загружает конкретный экземпляр по его индексу и обновляет изображение.
        /// Зум сбрасывается на 100%.
        /// </summary>
        private async Task LoadAndDisplayInstanceAsync(int index)
        {
            if (index < 0 || index >= _instanceIds.Count) return;
            string instId = _instanceIds[index];

            try
            {
                // Формируем URL с WW/WL, если указаны
                string url = $"api/studies/{_studyId}/series/{_seriesId}/instances/{instId}/preview";
                bool hasWw = int.TryParse(txtWindowWidth.Text.Trim(), out int ww);
                bool hasWl = int.TryParse(txtWindowLevel.Text.Trim(), out int wl);

                if (hasWw && hasWl)
                    url += $"?ww={ww}&wl={wl}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();
                using var ms = new System.IO.MemoryStream(pngBytes);
                var loaded = new Bitmap(ms);

                // Запомним оригинал и сбросим зум
                _originalImage = new Bitmap(loaded);
                _zoomFactor    = 1.0f;

                // Сгенерировать текущее масштабированное изображение
                RecalculateCurrentScaledImage();
                // Обновим lblTotalFrames (если надо)
                lblTotalFrames.Text = $"/ {_instanceIds.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить превью кадра:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Пересчёт _currentScaledImage на основе _originalImage и _zoomFactor.
        /// Затем перерисовка picBox.
        /// </summary>
        private void RecalculateCurrentScaledImage()
        {
            if (_originalImage == null)
                return;

            // Удалим предыдущий, если есть
            _currentScaledImage?.Dispose();

            int newW = (int)(_originalImage.Width * _zoomFactor);
            int newH = (int)(_originalImage.Height * _zoomFactor);

            // Создаём новую Bitmap для отрисовки
            _currentScaledImage = new Bitmap(_originalImage, new Size(newW, newH));

            // Перерисуем
            picBox.Invalidate();
        }

        /// <summary>
        /// Обработчик Paint для picBox:
        /// рисуем _currentScaledImage, центрируя внутри picBox.
        /// </summary>
        private void PicBox_Paint(object? sender, PaintEventArgs e)
        {
            if (_currentScaledImage == null)
                return;

            int sW = _currentScaledImage.Width;
            int sH = _currentScaledImage.Height;

            // Смещение, чтобы картинка была по центру picBox
            int dx = (picBox.ClientSize.Width  - sW) / 2;
            int dy = (picBox.ClientSize.Height - sH) / 2;

            // Если картинка меньше picBox, центруем; иначе будет обрезана
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_currentScaledImage, dx, dy, sW, sH);
        }

        /// <summary>
        /// Обработчик MouseWheel — меняем _zoomFactor и пересчитываем _currentScaledImage.
        /// </summary>
        private void PicBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_originalImage == null) return;

            if (e.Delta > 0)
                _zoomFactor = Math.Min(_zoomFactor + ZoomStep, MaxZoom);
            else
                _zoomFactor = Math.Max(_zoomFactor - ZoomStep, MinZoom);

            RecalculateCurrentScaledImage();
        }
    }
}
