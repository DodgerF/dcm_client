using System;
using System.Windows.Forms;

namespace client
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    string jwtToken = loginForm.JwtToken;
                    string userRole = loginForm.UserRole;

                    if (userRole == "ROLE_ADMIN")
                    {
                        Application.Run(new AdminForm(jwtToken));
                    }
                    else
                    {
                        Application.Run(new StartForm(jwtToken, userRole));
                    }
                }
                else
                {
                    Application.Exit();
                }
            }
        }
    }
}
