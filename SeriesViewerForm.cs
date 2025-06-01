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

        // Label для отображения координат внутри picBox
        private Label      lblMouseCoords = null!;

        private ComboBox   cmbPresets = null!;
        private Label      lblWW = null!;
        private TextBox    txtWindowWidth = null!;
        private Label      lblSlash = null!;
        private Label      lblWL = null!;
        private TextBox    txtWindowLevel = null!;

        // Кнопки HU, R, S
        private Button     btnHU = null!;
        private Button     btnR  = null!;
        private Button     btnS  = null!;

        // TextBox для результата HU (ReadOnly)
        private TextBox    txtHuResult = null!;

        // Для зума/панорамы: оригинал и текущая версия картинки
        private Bitmap?    _originalImage;
        private Bitmap?    _currentScaledImage;
        private float      _zoomFactor = 1.0f;
        private const float ZoomStep = 0.1f;
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 10.0f;

        // Смещение (offset) картинки внутри picBox для панорамирования
        private int _offsetX = 0;
        private int _offsetY = 0;

        // Флаги и данные для перетаскивания (панорамы)
        private bool _isPanning = false;
        private int  _panStartMouseX = 0;
        private int  _panStartMouseY = 0;
        private int  _panStartOffsetX = 0;
        private int  _panStartOffsetY = 0;

        // Предустановки WW/WL
        private readonly Dictionary<string, (int WW, int WL)> _windowPresets =
            new Dictionary<string, (int, int)>
            {
                { "Мягкие ткани",      (350,   40)   },
                { "Костные ткани",     (2000,  400)  },
                { "Лёгкие",            (1500, -600)  },
                { "Пользовательские",  (0,     0)    }
            };

        // Список instanceId-ов этой серии
        private List<string> _instanceIds = new List<string>();

        // Флаг режима «HU»
        private bool _huMode = false;

        // Храним координаты маркера в пикселях оригинального изображения
        private int? _markerImgX = null;
        private int? _markerImgY = null;

        public SeriesViewerForm(HttpClient httpClient, string studyId, string seriesId, string seriesName)
        {
            _httpClient = httpClient;
            _studyId    = studyId;
            _seriesId   = seriesId;

            Text = $"Просмотр серии: {seriesName}";
            ClientSize = new Size(820, 760);
            MinimumSize = new Size(820, 760);

            InitializeControls();
            AttachEvents();

            _ = LoadInstanceListAsync();
        }

        private void InitializeControls()
        {
            //
            // 1. PictureBox: фиксированный размер 780×520
            //
            picBox = new PictureBox
            {
                Location    = new Point(10, 10),
                Size        = new Size(780, 520),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.Black,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left
            };
            picBox.MouseWheel += PicBox_MouseWheel;
            picBox.Paint      += PicBox_Paint;
            picBox.MouseClick += PicBox_MouseClick;
            picBox.MouseDown  += PicBox_MouseDown;
            picBox.MouseMove  += PicBox_MouseMove;
            picBox.MouseUp    += PicBox_MouseUp;

            // Label для координат (дочерний элемент picBox)
            lblMouseCoords = new Label
            {
                AutoSize  = true,
                BackColor = Color.FromArgb(160, Color.Black),
                ForeColor = Color.White,
                Text      = "(X: -, Y: -)",
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right
            };
            // Начальное позиционирование: 15px слева от правого края, 5px снизу
            lblMouseCoords.Location = new Point(
                picBox.ClientSize.Width - lblMouseCoords.Width - 15,
                picBox.ClientSize.Height - lblMouseCoords.Height - 5
            );
            picBox.Controls.Add(lblMouseCoords);

            Controls.Add(picBox);

            //
            // 2. TrackBar под PictureBox
            //
            trackBar = new TrackBar
            {
                Location   = new Point(10, picBox.Bottom + 10),
                Size       = new Size(780, 45),
                Minimum    = 0,
                Maximum    = 0,
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
                Size      = new Size(50, 20),
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

            //
            // --- Кнопки HU, R, S ---
            //
            int buttonsY = txtWindowLevel.Bottom + 20;
            btnHU = new Button
            {
                Location = new Point(10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "HU",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnHU);

            btnR = new Button
            {
                Location = new Point(btnHU.Right + 10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "R",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnR);

            btnS = new Button
            {
                Location = new Point(btnR.Right + 10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "S",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnS);

            //
            // 11. TextBox для результата HU (ReadOnly)
            //
            txtHuResult = new TextBox
            {
                Location  = new Point(10, btnHU.Bottom + 15),
                Size      = new Size(400, 25),
                ReadOnly  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(txtHuResult);
        }

        private void AttachEvents()
        {
            //
            // TrackBar: загрузка кадра при прокрутке
            //
            trackBar.Scroll += async (_, __) =>
            {
                int idx = trackBar.Value;
                txtFrameIndex.Text = (idx + 1).ToString();
                await LoadAndDisplayInstanceAsync(idx);
            };

            //
            // Enter в txtFrameIndex
            //
            txtFrameIndex.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled          = true;
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
            // MouseMove внутри picBox: обновляем lblMouseCoords
            //
            picBox.MouseMove += (sender, e) =>
            {
                if (_currentScaledImage == null || _originalImage == null)
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                    return;
                }

                int sW = _currentScaledImage.Width;
                int sH = _currentScaledImage.Height;

                // Координаты курсора относительно области, где рисуется картинка:
                int relX = e.X - _offsetX;
                int relY = e.Y - _offsetY;

                if (relX >= 0 && relX < sW && relY >= 0 && relY < sH)
                {
                    // Преобразуем в координаты оригинала
                    float imgXf = relX / _zoomFactor;
                    float imgYf = relY / _zoomFactor;

                    int ix = Math.Min(Math.Max((int)imgXf, 0), _originalImage.Width - 1);
                    int iy = Math.Min(Math.Max((int)imgYf, 0), _originalImage.Height - 1);

                    lblMouseCoords.Text = $"(X: {ix}, Y: {iy})";
                }
                else
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                }
            };

            //
            // MouseWheel для зума
            //
            picBox.MouseWheel += PicBox_MouseWheel;

            //
            // Paint для отрисовки масштабированного изображения и маркера
            //
            picBox.Paint += PicBox_Paint;

            //
            // MouseDown/MouseMove/MouseUp для панорамы
            //
            picBox.MouseDown += PicBox_MouseDown;
            picBox.MouseMove += PicBox_MouseMove;
            picBox.MouseUp   += PicBox_MouseUp;

            //
            // Resize picBox: пересчитываем центр/панораму картинки
            //
            picBox.Resize += (sender, e) =>
            {
                CenterImageInView();
                picBox.Invalidate();
            };

            //
            // Смена пресетов WW/WL
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
            // Ввод в WW/WL вручную → переключаемся на «Пользовательские»
            //
            txtWindowWidth.TextChanged += (sender, e) => OnWindowTextChanged();
            txtWindowLevel.TextChanged += (sender, e) => OnWindowTextChanged();

            //
            // Кнопка HU: включаем режим, остальные — выключаем
            //
            btnHU.Click += (sender, e) =>
            {
                _huMode = true;
                btnHU.BackColor = Color.LightBlue;
                btnR.BackColor = SystemColors.Control;
                btnS.BackColor = SystemColors.Control;
                ClearMarkerAndResult();
            };

            //
            // Кнопка R: выключаем HU-режим
            //
            btnR.Click += (sender, e) =>
            {
                _huMode = false;
                btnR.BackColor = Color.LightBlue;
                btnHU.BackColor = SystemColors.Control;
                btnS.BackColor = SystemColors.Control;
                ClearMarkerAndResult();
            };

            //
            // Кнопка S: выключаем HU-режим
            //
            btnS.Click += (sender, e) =>
            {
                _huMode = false;
                btnS.BackColor = Color.LightBlue;
                btnHU.BackColor = SystemColors.Control;
                btnR.BackColor = SystemColors.Control;
                ClearMarkerAndResult();
            };

            //
            // Resize формы: trackBar и поля смещаются по Anchor,
            // но картинка и маркер требуют перерисовки
            //
            this.Resize += (sender, e) =>
            {
                CenterImageInView();
                picBox.Invalidate();
            };
        }

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

        private async Task LoadAndDisplayInstanceAsync(int index)
        {
            if (index < 0 || index >= _instanceIds.Count) return;
            string instId = _instanceIds[index];

            try
            {
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

                _originalImage = new Bitmap(loaded);
                _zoomFactor    = 1.0f;

                // Центрируем картинку в picBox
                RecalculateCurrentScaledImage();
                CenterImageInView();

                ClearMarkerAndResult();
                lblTotalFrames.Text = $"/ {_instanceIds.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить превью кадра:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Создаёт _currentScaledImage из _originalImage согласно _zoomFactor.
        /// </summary>
        private void RecalculateCurrentScaledImage()
        {
            if (_originalImage == null)
                return;

            _currentScaledImage?.Dispose();
            int newW = (int)(_originalImage.Width * _zoomFactor);
            int newH = (int)(_originalImage.Height * _zoomFactor);

            _currentScaledImage = new Bitmap(_originalImage, new Size(newW, newH));
            picBox.Invalidate();
        }

        /// <summary>
        /// Центрирует картинку внутри picBox, если она меньше.
        /// Если картинка больше, просто оставляет текущее смещение.
        /// </summary>
        private void CenterImageInView()
        {
            if (_currentScaledImage == null)
                return;

            int imgW = _currentScaledImage.Width;
            int imgH = _currentScaledImage.Height;

            // Если картинка уже меньше picBox, центрируем...
            if (imgW < picBox.ClientSize.Width)
                _offsetX = (picBox.ClientSize.Width - imgW) / 2;
            // Если картинка шире, не меняем _offsetX (иначе она «съедет»).
            if (imgH < picBox.ClientSize.Height)
                _offsetY = (picBox.ClientSize.Height - imgH) / 2;
        }

        /// <summary>
        /// Рисует масштабированное изображение и, если есть, квадратный маркер.
        /// </summary>
        private void PicBox_Paint(object? sender, PaintEventArgs e)
        {
            if (_currentScaledImage == null || _originalImage == null)
                return;

            // 1) Рисуем масштабированное изображение по (_offsetX, _offsetY)
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_currentScaledImage, _offsetX, _offsetY, _currentScaledImage.Width, _currentScaledImage.Height);

            // 2) Если есть маркер, рисуем квадрат 10×10 поверх картинки
            if (_markerImgX != null && _markerImgY != null)
            {
                int imgX = _markerImgX.Value;
                int imgY = _markerImgY.Value;

                // Переводим координаты пикселя оригинала в экранные, с учётом зума и смещения
                float screenXf = _offsetX + imgX * _zoomFactor;
                float screenYf = _offsetY + imgY * _zoomFactor;

                int size = 10; // размер квадрата-маркера
                int half = size / 2;
                var rect = new Rectangle(
                    (int)screenXf - half,
                    (int)screenYf - half,
                    size,
                    size
                );
                using var brush = new SolidBrush(Color.Red);
                e.Graphics.FillRectangle(brush, rect);
            }
        }

        /// <summary>
        /// Обработчик MouseWheel: меняем зум и пересчитываем изображение.
        /// Сохраняем текущее смещение, но после пересчёта надо убедиться,
        /// что картинка не ушла за границы.
        /// </summary>
        private void PicBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_originalImage == null)
                return;

            // Запомним координаты курсора в относительных пикселях до зума
            // для того, чтобы приблизиться к тому же месту после зума.
            int oldImgX = (int)((e.X - _offsetX) / _zoomFactor);
            int oldImgY = (int)((e.Y - _offsetY) / _zoomFactor);

            if (e.Delta > 0)
                _zoomFactor = Math.Min(_zoomFactor + ZoomStep, MaxZoom);
            else
                _zoomFactor = Math.Max(_zoomFactor - ZoomStep, MinZoom);

            RecalculateCurrentScaledImage();

            // После пересчёта скорректируем смещение так, чтобы курсор «остался» на той же точке изображения
            int newScreenX = (int)(oldImgX * _zoomFactor);
            int newScreenY = (int)(oldImgY * _zoomFactor);

            _offsetX = e.X - newScreenX;
            _offsetY = e.Y - newScreenY;

            // Но защитимся от ситуации, когда картинка меньше picBox: в этом случае центруем
            CenterImageInView();

            picBox.Invalidate();
        }

        /// <summary>
        /// Обрабатываем MouseDown: если левая кнопка и не в HU-режиме (или даже в HU-режиме),
        /// начинаем панораму.
        /// </summary>
        private void PicBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPanning = true;
                _panStartMouseX = e.X;
                _panStartMouseY = e.Y;
                _panStartOffsetX = _offsetX;
                _panStartOffsetY = _offsetY;
                picBox.Cursor = Cursors.Hand;
            }
        }

        /// <summary>
        /// При движении мыши с зажатой левой кнопкой корректируем смещение (панораму).
        /// </summary>
        private void PicBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isPanning && _currentScaledImage != null)
            {
                int dx = e.X - _panStartMouseX;
                int dy = e.Y - _panStartMouseY;
                _offsetX = _panStartOffsetX + dx;
                _offsetY = _panStartOffsetY + dy;

                // Проверим границы: чтобы нельзя было « показать пустоту » за краем 
                int imgW = _currentScaledImage.Width;
                int imgH = _currentScaledImage.Height;
                int boxW = picBox.ClientSize.Width;
                int boxH = picBox.ClientSize.Height;

                // Если картинка шире области: ограничиваем _offsetX так, чтобы ее край не вылезал
                if (imgW > boxW)
                {
                    int minX = boxW - imgW; // максимально «влево»
                    int maxX = 0;           // максимально «вправо»
                    _offsetX = Math.Min(Math.Max(_offsetX, minX), maxX);
                }
                else
                {
                    // Если картинка уже меньше, чем box, просто центрируем
                    _offsetX = (boxW - imgW) / 2;
                }

                // Аналогично для высоты
                if (imgH > boxH)
                {
                    int minY = boxH - imgH;
                    int maxY = 0;
                    _offsetY = Math.Min(Math.Max(_offsetY, minY), maxY);
                }
                else
                {
                    _offsetY = (boxH - imgH) / 2;
                }

                picBox.Invalidate();
            }
        }

        /// <summary>
        /// По MouseUp завершаем панораму.
        /// </summary>
        private void PicBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _isPanning)
            {
                _isPanning = false;
                picBox.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// При клике: если HU-режим, вычисляем координаты, рисуем квадрат и посылаем запрос.
        /// После клика HU-режим выключается.
        /// </summary>
        private async void PicBox_MouseClick(object? sender, MouseEventArgs e)
        {
            if (!_huMode || _currentScaledImage == null || _originalImage == null)
                return;

            int imgW = _currentScaledImage.Width;
            int imgH = _currentScaledImage.Height;

            // Локальные координаты клика в области изображения:
            int relX = e.X - _offsetX;
            int relY = e.Y - _offsetY;
            if (relX < 0 || relX >= imgW || relY < 0 || relY >= imgH)
                return;

            float imgXf = relX / _zoomFactor;
            float imgYf = relY / _zoomFactor;
            int imgX = Math.Min(Math.Max((int)imgXf, 0), _originalImage.Width - 1);
            int imgY = Math.Min(Math.Max((int)imgYf, 0), _originalImage.Height - 1);

            // Сохраняем координаты маркера
            _markerImgX = imgX;
            _markerImgY = imgY;
            picBox.Invalidate();

            // Отключаем HU-режим сразу
            _huMode = false;
            btnHU.BackColor = SystemColors.Control;

            int idx = trackBar.Value;
            string instId = _instanceIds[idx];

            try
            {
                string reqUrl = $"api/studies/instances/{instId}/density?x={imgX}&y={imgY}";
                var response = await _httpClient.GetAsync(reqUrl);
                if (response.IsSuccessStatusCode)
                {
                    int huValue = await response.Content.ReadFromJsonAsync<int>();
                    txtHuResult.Text = $"Плотность ткани в точке равна {huValue} HU";
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Сервер вернул ошибку при получении HU:\n{error}",
                        "Ошибка HU",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    txtHuResult.Text = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при запросе HU на сервер:\n{ex.Message}",
                    "Ошибка HU",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                txtHuResult.Text = "";
            }
        }

        private void ClearMarkerAndResult()
        {
            _markerImgX = null;
            _markerImgY = null;
            txtHuResult.Text = "";
            picBox.Invalidate();
        }
    }
}
