using Microsoft.Data.SqlClient;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Syncfusion.EJ2.Notifications;
namespace DoAn.Areas.Admin.Database
{
    public class DBconnect
        {
        //Data Source = LAPTOP - HCQ7FVIS\\SQLEXPRESS;Initial Catalog = CuoiKy_LanCuoi; Integrated Security = True; Trust Server Certificate=True
        //to create connection 
        //workstation id=hyvongthanhcong.mssql.somee.com;packet size=4096;user id=thien123_SQLLogin_1;pwd=bm3urcod9v;data source=hyvongthanhcong.mssql.somee.com;persist security info=False;initial catalog=hyvongthanhcong;TrustServerCertificate=True
        SqlConnection connect = new SqlConnection("Data Source = LAPTOP - HCQ7FVIS\\SQLEXPRESS;Initial Catalog = CuoiKy_LanCuoi; Integrated Security = True; Trust Server Certificate=True");

            //to get connection 
            public SqlConnection getConnecttion()
            {
                return connect;

            }

            //create a function to Open connection 
            public void openConnection()
            {
                if (connect.State == System.Data.ConnectionState.Closed)
                {
                    connect.Open();
                }
            }

            //create a function to Close connection 
            public void closeConnection()
            {
                if (connect.State == System.Data.ConnectionState.Open)
                {
                    connect.Close();
                }
            }
        }
}
