using FYJ;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace FYJ.Data.Min
{

    public delegate object NewIDHandler();
    /// <summary>
    /// 提供实体通用的基本操作方法
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class EntityHelper<T>
        where T : new()
    {



        private static bool IsPrimary(string key, params string[] primaryKey)
        {
            foreach (string p in primaryKey)
            {
                if ((p.Equals(key, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static object ConvertDbObject(object obj)
        {
            if (obj == null)
            {
                return DBNull.Value;
            }
            else
            {
                if (obj.GetType() == typeof(DateTime))
                {
                    if (Convert.ToDateTime(obj) < new DateTime(1753, 1, 1) || Convert.ToDateTime(obj) > new DateTime(9999, 12, 31))
                    {
                        return DBNull.Value;
                    }
                    else
                    {
                        return obj;
                    }
                }
                else
                {
                    return obj;
                }
            }
        }

        #region  新增
        public static int Insert(T entity, string tableName, string primaryKey, bool isAddPrimaryKey)
        {
            return Insert(DbHelper.Instance, entity, tableName, primaryKey, isAddPrimaryKey);
        }
        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="db"></param>
        /// <param name="entity"></param>
        /// <param name="tableName">表名</param>
        /// <param name="primaryKey">主键</param>
        /// <param name="isAddPrimaryKey">是否插入主键</param>
        /// <returns></returns>
        public static int Insert(DbHelper db, T entity, string tableName, string primaryKey, bool isAddPrimaryKey)
        {
            PropertyInfo[] pis = entity.GetType().GetProperties();

            String sql = "insert into {0} ({1}) values ({2})";
            String col = "";
            String val = "";
            List<DbParameter> parames = new List<DbParameter>();

            foreach (PropertyInfo pi in pis)
            {
                object[] att = pi.GetCustomAttributes(typeof(IgnoreAttribute), false);
                if (att != null && att.Length > 0)
                {
                    continue;
                }

                string parameterName = pi.Name;
                DbParameter parameter = db.CreateParameter();
                object obj = pi.GetValue(entity, null);

                //如果不增加主键 则排除
                if (!isAddPrimaryKey)
                {
                    if (parameterName.Equals(primaryKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }
                }

                col += parameterName + ",";
                val += db.ParameterPrefix + parameterName + ",";
                parameter.ParameterName = db.ParameterPrefix + parameterName;
                parameter.Value = ConvertDbObject(obj);
                parames.Add(parameter);
            }
            col = col.TrimEnd(',');
            val = val.TrimEnd(',');
            sql = String.Format(sql, tableName, col, val);

            return db.ExecuteSql(sql, parames.ToArray());
        }

        #endregion

        #region  修改
        public static int Update(T entity, string tableName, params string[] primaryKey)
        {
            return Update(DbHelper.Instance, entity, tableName, primaryKey);
        }
        /// <summary>
        /// 修改数据
        /// </summary>
        /// <param name="db">新实体</param>
        /// <param name="entity">新实体</param>
        /// <param name="tableName">表名</param>
        /// <param name="primaryKey">主键,可以多个</param>
        /// <returns></returns>
        public static int Update(DbHelper db, T entity, string tableName, params string[] primaryKey)
        {

            String col = "";
            List<DbParameter> parames = new List<DbParameter>();
            PropertyInfo[] pis = entity.GetType().GetProperties();
            foreach (PropertyInfo pi in pis)
            {
                object[] att = pi.GetCustomAttributes(typeof(IgnoreAttribute), false);
                if (att != null && att.Length > 0)
                {
                    continue;
                }

                string parameterName = pi.Name;
                object obj = pi.GetValue(entity, null);  //获取属性值
                //如果不是主键
                if (!IsPrimary(parameterName, primaryKey))
                {
                    DbParameter parameter = db.CreateParameter();
                    col += parameterName + "=" + db.ParameterPrefix + parameterName + ",";
                    parameter.ParameterName = db.ParameterPrefix + parameterName;
                    parameter.Value = ConvertDbObject(obj);
                    parames.Add(parameter);
                }
                else
                {
                    DbParameter parameter = db.CreateParameter();
                    parameter.ParameterName = db.ParameterPrefix + parameterName;
                    parameter.Value = obj;
                    parames.Add(parameter);
                }

            }
            col = col.TrimEnd(',');

            string tmp = "";
            foreach (string p in primaryKey)
            {
                tmp += " and " + p + "=@" + p;
            }

            string sql = "update " + tableName + " set " + col + " where 1=1 " + tmp;

            return db.ExecuteSql(sql, parames.ToArray());
        }
        #endregion

        public static bool IsExists(T entity, string tableName, params string[] primaryKey)
        {
            return IsExists(DbHelper.Instance,entity,tableName,primaryKey);
        }
        /// <summary>
        /// 是否存在
        /// </summary>
        /// <param name="db"></param>
        /// <param name="entity"></param>
        /// <param name="tableName"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public static bool IsExists(DbHelper db, T entity, string tableName, params string[] primaryKey)
        {
            List<DbParameter> parames = new List<DbParameter>();
            string sql = "select 1 from " + tableName + " where 1=1 ";
            foreach (string s in primaryKey)
            {
                sql += " and " + s + "=" + db.ParameterPrefix + s;
                object value = entity.GetType().GetProperty(s).GetValue(entity, null);
                DbParameter parameter = db.CreateParameter();
                parameter.ParameterName = db.ParameterPrefix + s;
                parameter.Value = value;
                parames.Add(parameter);
            }


            DataTable dt = db.GetDataTable(sql, parames.ToArray());
            if (dt.Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static T GetEntity( string tableName, string key, string value)
        {
            return GetEntity(DbHelper.Instance,tableName,key,value);
        }

        /// <summary>
        /// 获取一条实体数据
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T GetEntity(DbHelper db, string tableName, string key, string value)
        {
            T model = default(T);
            string sql = "SELECT * FROM " + tableName + " WHERE " + key + "=" + db.ParameterPrefix + key;
            DataTable dt = db.GetDataTable(sql, db.CreateParameter(db.ParameterPrefix + key, value));
            if (dt.Rows.Count == 1)
            {
                model = ObjectHelper.DataTableToSingleModel<T>(dt);
            }
            return model;
        }
    }
}
