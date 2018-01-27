using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FYJ.Data.Min
{
    public class DbHelper
    {
        private DbProviderFactory _factory;
        private string _connectionString;

        private string _ParameterPrefix = "@";
        /// <summary>
        /// SQL参数前缀
        /// </summary>
        public string ParameterPrefix
        {
            get { return _ParameterPrefix; }
        }

        private static DbHelper _dbHelper;
        public static DbHelper Instance
        {
            get
            {
                if (_dbHelper == null)
                {
                    //Configuration config = ConfigurationManager.OpenExeConfiguration(Path.Combine(AppDomain.CurrentDomain.RelativeSearchPath, "FYJ.Data.Min.dll"));
                    //string providerName = config.ConnectionStrings.ConnectionStrings["FYJ.Data.Min"].ProviderName;
                    //string connectionString = config.ConnectionStrings.ConnectionStrings["FYJ.Data.Min"].ConnectionString;
                    //if (config.AppSettings.Settings["IsEncrypt"].Value.ToLower() == "true")
                    //{
                    //    connectionString = Decrypt(connectionString);
                    //}

                    string providerName = System.Configuration.ConfigurationManager.ConnectionStrings["FYJ.Data.Min"].ProviderName;
                    string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["FYJ.Data.Min"].ConnectionString;
                    _dbHelper = new DbHelper(providerName, connectionString);
                }

                return _dbHelper;
            }
        }

        public DbHelper(DbProviderFactory factory, string connectionString)
        {
            this._factory = factory;
            this._connectionString = connectionString;
        }

        public DbHelper(string configName)
        {
            string providerName = System.Configuration.ConfigurationManager.ConnectionStrings[configName].ProviderName;
            if (providerName.StartsWith("MySql.Data", StringComparison.CurrentCultureIgnoreCase))
            {
                object obj = Assembly.Load("MySql.Data").CreateInstance("MySql.Data.MySqlClient.MySqlClientFactory");
                this._factory = (DbProviderFactory)obj;
            }
            else if (providerName.StartsWith("System.Data.SQLite", StringComparison.CurrentCultureIgnoreCase))
            {
                object obj = Assembly.Load("System.Data.SQLite").CreateInstance("System.Data.SQLite.SQLiteFactory");
                this._factory = (DbProviderFactory)obj;
            }
            else
            {
                this._factory = DbProviderFactories.GetFactory(providerName);
            }
            this._connectionString = System.Configuration.ConfigurationManager.ConnectionStrings[configName].ConnectionString;
        }

        public DbHelper(string providerName, string connectionString)
        {
            if (providerName.StartsWith("MySql.Data", StringComparison.CurrentCultureIgnoreCase))
            {
                object obj = Assembly.Load("MySql.Data").CreateInstance("MySql.Data.MySqlClient.MySqlClientFactory");
                this._factory = (DbProviderFactory)obj;
            }
            else if (providerName.StartsWith("System.Data.SQLite", StringComparison.CurrentCultureIgnoreCase))
            {
                object obj = Assembly.Load("System.Data.SQLite").CreateInstance("System.Data.SQLite.SQLiteFactory");
                this._factory = (DbProviderFactory)obj;
            }
            else
            {
                this._factory = DbProviderFactories.GetFactory(providerName);
            }
            this._connectionString = connectionString;
        }


        private void GeneralKeyIV(string keyStr, out byte[] key, out byte[] iv)
        {
            //RijndaelManaged rDel = new RijndaelManaged();
            byte[] bytes = Encoding.UTF8.GetBytes(keyStr);
            key = SHA256Managed.Create().ComputeHash(bytes);
            iv = MD5.Create().ComputeHash(bytes);
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string Encrypt(string text)
        {
            string sKey = "fangyuanjun";
            byte[] inputByteArray = Encoding.UTF8.GetBytes(text);

            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

            byte[] _key;
            byte[] _iv;
            GeneralKeyIV(sKey, out _key, out _iv);
            aes.Key = _key;
            aes.IV = _iv;

            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();

            string result = Convert.ToBase64String(ms.ToArray());
            ms.Dispose();
            cs.Dispose();

            return result;
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="text">要解密的字符串</param>
        /// <returns></returns>
        public string Decrypt(string text)
        {
            string sKey = "fangyuanjun";
            byte[] inputByteArray = Convert.FromBase64String(text);

            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();

            byte[] _key;
            byte[] _iv;
            GeneralKeyIV(sKey, out _key, out _iv);
            aes.Key = _key;
            aes.IV = _iv;

            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(inputByteArray, 0, inputByteArray.Length);
            cs.FlushFinalBlock();
            cs.Close();
            string str = Encoding.UTF8.GetString(ms.ToArray());
            ms.Close();

            return str;
        }


        /// <summary>
        /// 构造参数
        /// </summary>
        /// <param name="parameterName">[可选参数]参数名</param>
        /// <param name="parameterValue">[可选参数]参数值</param>
        /// <param name="direction">[可选参数]参数类型</param>
        /// <returns></returns>
        public DbParameter CreateParameter(string parameterName = null, object parameterValue = null, ParameterDirection? direction = null)
        {
            DbParameter paramter = this._factory.CreateParameter();
            if (parameterName != null)
            {
                paramter.ParameterName = parameterName;
            }
            if (parameterValue != null)
            {
                paramter.Value = parameterValue;
            }
            if (direction != null)
            {
                paramter.Direction = direction.Value;
            }
            return paramter;
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataSet GetDataSet(string sql, params IDataParameter[] parameters)
        {
            DbConnection conn = this._factory.CreateConnection();
            conn.ConnectionString = this._connectionString;
            conn.Open();
            DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            DbDataAdapter adapter = this._factory.CreateDataAdapter();
            if (parameters != null)
            {
                foreach (IDataParameter parm in parameters)
                {
                    cmd.Parameters.Add(parm);
                }
            }
            adapter.SelectCommand = cmd;
            DataSet ds = new DataSet();
            adapter.Fill(ds);
            cmd.Parameters.Clear();
            conn.Close();

            return ds;
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataTable GetDataTable(string sql, params IDataParameter[] parameters)
        {
            DataSet ds = GetDataSet(sql, parameters);
            return ds.Tables[0];
        }

        /// <summary>
        /// 是否存在
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool IsExists(string sql, params IDataParameter[] parameters)
        {
            DataTable dt = GetDataTable(sql, parameters);
            return dt.Rows.Count > 0;
        }

        /// <summary>
        /// 执行sql
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int ExecuteSql(string sql, params IDataParameter[] parameters)
        {
            DbConnection conn = null;
            try
            {
                conn = this._factory.CreateConnection();
                conn.ConnectionString = this._connectionString;
                conn.Open();
                DbCommand cmd = conn.CreateCommand();
                if (parameters != null)
                {
                    foreach (DbParameter parameter in parameters)
                    {
                        if ((parameter.Direction == ParameterDirection.InputOutput || parameter.Direction == ParameterDirection.Input) &&
                            (parameter.Value == null))
                        {
                            parameter.Value = DBNull.Value;
                        }
                        cmd.Parameters.Add(parameter);
                    }
                }
                cmd.CommandText = sql;
                int rows = cmd.ExecuteNonQuery();

                return rows;
            }
            catch
            {
                throw;
            }
            finally
            {
                if(conn!=null)
                {
                    conn.Close();
                }
            }
            
        }

        /// <summary>
        /// 执行带事务的sql   使用后需要手动关闭数据库
        /// </summary>
        /// <param name="tran"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int ExecuteSql(IDbTransaction tran, string sql, params IDataParameter[] parameters)
        {
            IDbConnection conn = tran.Connection;
            IDbCommand cmd = conn.CreateCommand();
            cmd.Transaction = tran;
            if (parameters != null)
            {
                foreach (DbParameter parameter in parameters)
                {
                    if ((parameter.Direction == ParameterDirection.InputOutput || parameter.Direction == ParameterDirection.Input) &&
                        (parameter.Value == null))
                    {
                        parameter.Value = DBNull.Value;
                    }
                    cmd.Parameters.Add(parameter);
                }
            }
            cmd.CommandText = sql;
            int rows = cmd.ExecuteNonQuery();

            return rows;
        }


        public  string SqlFilter(string str)
        {
            if(String.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            List<Regex> regList = new List<Regex>();
            regList.Add(new Regex(@"select", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"insert", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"delete", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"update", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"drop", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"exec", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"truncate", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"'", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@";", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"\s+and\s+", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"\s+or\s+", RegexOptions.IgnoreCase));
            regList.Add(new Regex(@"<>", RegexOptions.IgnoreCase));
            foreach (Regex reg in regList)
            {
                str = reg.Replace(str, "");
            }

            return str;
        }

    }
}
