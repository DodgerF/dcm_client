namespace client;

using System;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;


public class Form1 : Form
{
    private Button buttonFetch;
    private PictureBox pictureBox1;
    private readonly HttpClient _httpClient;

    public Form1()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    private void InitializeComponent()
    {
        buttonFetch = new Button();
        pictureBox1 = new PictureBox();

        buttonFetch.Location = new Point(12, 12);
        buttonFetch.Size = new Size(120, 30);
        buttonFetch.Text = "Получить картинку";
        buttonFetch.Click += new EventHandler(buttonFetch_Click);

        pictureBox1.Location = new Point(12, 50);
        pictureBox1.Size = new Size(500, 400);
        pictureBox1.BorderStyle = BorderStyle.FixedSingle;
        pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;

        ClientSize = new Size(530, 470);
        Controls.Add(buttonFetch);
        Controls.Add(pictureBox1);
        Text = "DICOM Viewer";
    }

    private async void buttonFetch_Click(object? sender, EventArgs e)
    {
        try
        {
            string endpoint = "http://localhost:8080/dicom?file=example.dcm";
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();

            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                string imageUrl = doc.RootElement.GetProperty("url").GetString();

                HttpResponseMessage imageResponse = await _httpClient.GetAsync(imageUrl);
                imageResponse.EnsureSuccessStatusCode();

                using (var stream = await imageResponse.Content.ReadAsStreamAsync())
                {
                    Image image = Image.FromStream(stream);
                    pictureBox1.Image = image;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

