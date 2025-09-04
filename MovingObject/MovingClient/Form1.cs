using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MovingClient
{
    public partial class Form1 : Form
    {
        private TcpClient? client;
        private NetworkStream? stream;
        private string serverIp = "127.0.0.1";
        private int port = 5000;
        private PictureBox pictureBox;

        public Form1()
        {
            InitializeComponent();
            this.Text = "Client - MovingObject";
            this.ClientSize = new Size(400, 360);

            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            this.Controls.Add(pictureBox);

            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            Task.Run(() => ConnectLoop());
        }

        private async Task ConnectLoop()
        {
            while (true)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync(serverIp, port);
                    stream = client.GetStream();
                    Console.WriteLine($"Connected to server {serverIp}:{port}");
                    await ReceiveLoop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Connection error: " + ex.Message);
                    await Task.Delay(1000);
                }
                finally
                {
                    try { stream?.Close(); client?.Close(); } catch { }
                }
            }
        }

        private async Task ReceiveLoop()
        {
            var header = new byte[4];
            while (client != null && client.Connected)
            {
                int h = await ReadExactAsync(stream!, header, 0, 4);
                if (h == 0) break;
                if (BitConverter.IsLittleEndian) Array.Reverse(header);

                int len = BitConverter.ToInt32(header, 0);
                if (len <= 0) break;

                var buffer = new byte[len];
                int got = await ReadExactAsync(stream!, buffer, 0, len);
                if (got == 0) break;

                using (var ms = new MemoryStream(buffer))
                {
                    var img = Image.FromStream(ms);
                    this.Invoke((Action)(() =>
                    {
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = new Bitmap(img);
                    }));
                }
            }
        }

        private async Task<int> ReadExactAsync(NetworkStream s, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int r = await s.ReadAsync(buffer, offset + total, count - total);
                if (r == 0) return 0;
                total += r;
            }
            return total;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            try { stream?.Close(); client?.Close(); } catch { }
        }
    }
}
