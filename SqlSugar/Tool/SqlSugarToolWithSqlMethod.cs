﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace OracleSugar
{
    /// <summary>
    /// SqlSugarTool局部类存放具有拼接SQL的函数(方便工具移植到其它数据库版本)
    /// </summary>
    public partial class SqlSugarTool
    {
        /// <summary>
        /// 将参数sql转成 '('+sql+')'
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static string PackagingSQL(string sql)
        {
            return string.Format("({0})", sql);
        }

        internal static StringBuilder GetQueryableSql<T>(Queryable<T> queryable)
        {
            string joinInfo = string.Join(" ", queryable.JoinTableValue);
            StringBuilder sbSql = new StringBuilder();
            string tableName = queryable.TableName.IsNullOrEmpty() ? queryable.TName : queryable.TableName;
            if (queryable.DB.Language.IsValuable() && queryable.DB.Language.Suffix.IsValuable())
            {
                var viewNameList = LanguageHelper.GetLanguageViewNameList(queryable.DB);
                var isLanView = viewNameList.IsValuable() && viewNameList.Any(it => it == tableName);
                if (!queryable.DB.Language.Suffix.StartsWith(LanguageHelper.PreSuffix))
                {
                    queryable.DB.Language.Suffix = LanguageHelper.PreSuffix + queryable.DB.Language.Suffix;
                }

                //将视图变更为多语言的视图
                if (isLanView)
                    tableName = typeof(T).Name + queryable.DB.Language.Suffix;
            }
            if (queryable.DB.PageModel == PageModel.RowNumber)
            {
                #region  rowNumber
                string withNoLock = queryable.DB.IsNoLock ? "" : null;
                var row = queryable.OrderByValue.IsValuable() ? (",ROWNUM row_index") : null;
                string orderBy = queryable.OrderByValue.IsValuable() ? ("ORDER BY " + queryable.OrderByValue) : null;
                sbSql.AppendFormat("SELECT " + queryable.SelectValue.GetSelectFiles(tableName) + " {1} FROM {0} {5} {2} WHERE 1=1 {3} {4} {6} ", tableName, row, withNoLock, string.Join("", queryable.WhereValue), queryable.GroupByValue.GetGroupBy(), joinInfo, orderBy);
                if (queryable.Skip == null && queryable.Take != null)
                {
                    if (joinInfo.IsValuable())
                    {
                        sbSql.Insert(0, "SELECT * FROM ( ");
                    }
                    else
                    {
                        sbSql.Insert(0, "SELECT " + queryable.SelectValue.GetSelectFiles() + " FROM ( ");
                    }
                    sbSql.Append(") t WHERE t.row_index<=" + queryable.Take);
                }
                else if (queryable.Skip != null && queryable.Take == null)
                {
                    if (joinInfo.IsValuable())
                    {
                        sbSql.Insert(0, "SELECT * FROM ( ");
                    }
                    else
                    {
                        sbSql.Insert(0, "SELECT " + queryable.SelectValue.GetSelectFiles() + " FROM ( ");
                    }
                    sbSql.Append(") t WHERE t.row_index>" + (queryable.Skip));
                }
                else if (queryable.Skip != null && queryable.Take != null)
                {
                    if (joinInfo.IsValuable())
                    {
                        sbSql.Insert(0, "SELECT * FROM ( ");
                    }
                    else
                    {
                        sbSql.Insert(0, "SELECT " + queryable.SelectValue.GetSelectFiles() + " FROM ( ");
                    }
                    sbSql.Append(") t WHERE t.row_index BETWEEN " + (queryable.Skip + 1) + " AND " + (queryable.Skip + queryable.Take));
                }
                #endregion
            }
            else
            {
            }
            return sbSql;
        }

        internal static void GetSqlableSql(Sqlable sqlable, string fileds, string orderByFiled, int pageIndex, int pageSize, StringBuilder sbSql)
        {
            if (sqlable.DB.PageModel == PageModel.RowNumber)
            {
                sbSql.Insert(0, string.Format("SELECT {0},ROWNUM row_index", fileds, orderByFiled));
                sbSql.Append(" WHERE 1=1 ").Append(string.Join(" ", sqlable.Where));
                sbSql.Append(sqlable.OrderBy);
                sbSql.Append(sqlable.GroupBy);
                int skip = (pageIndex - 1) * pageSize + 1;
                int take = pageSize;
                sbSql.Insert(0, "SELECT * FROM ( ");
                sbSql.AppendFormat(") t WHERE  t.row_index BETWEEN {0}  AND {1}   ", skip, skip + take - 1);
            }
        }

        /// <summary>
        /// 获取 WITH(NOLOCK)
        /// </summary>
        /// <param name="isNoLock"></param>
        /// <returns></returns>
        public static string GetLockString(bool isNoLock)
        {
            return isNoLock ? "" : "";
        }

        /// <summary>
        /// 根据表获取主键
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        internal static string GetPrimaryKeyByTableName(SqlSugarClient db, string tableName)
        {
            string key = "GetPrimaryKeyByTableName" + tableName;
            var cm = CacheManager<List<KeyValue>>.GetInstance();
            List<KeyValue> primaryInfo = null;

            //获取主键信息
            if (cm.ContainsKey(key))
                primaryInfo = cm[key];
            else
            {
                string sql = @"  				select cu.TABLE_NAME  ,cu.COLUMN_name KEYNAME  from user_cons_columns cu, user_constraints au 
   where cu.constraint_name = au.constraint_name
    and au.constraint_type = 'P' and au.table_name = '" + tableName+ @"'";
                var dt = db.GetDataTable(sql);
                primaryInfo = new List<KeyValue>();
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        primaryInfo.Add(new KeyValue() { Key = dr["TABLE_NAME"].ToString(), Value = dr["KEYNAME"].ToString() });
                    }
                }
                cm.Add(key, primaryInfo, cm.Day);
            }

            //反回主键
            if (!primaryInfo.Any(it => it.Key == tableName))
            {
                return null;
            }
            return primaryInfo.First(it => it.Key == tableName).Value;

        }

        /// <summary>
        ///根据表名获取自添列 keyTableName Value columnName
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        internal static List<KeyValue> GetIdentitiesKeyByTableName(SqlSugarClient db, string tableName)
        {
            if (OracleConfig.SequenceMapping.IsValuable())
            {
                return OracleConfig.SequenceMapping.Select(it => new KeyValue() { Key = it.TableName, Value = it.ColumnName }).ToList();
            }
            else
            {
                return new List<KeyValue>();
            }
        }

        /// <summary>
        /// 根据表名获取列名
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        internal static List<string> GetColumnsByTableName(SqlSugarClient db, string tableName)
        {
            string key = "GetColumnNamesByTableName" + tableName;
            var cm = CacheManager<List<string>>.GetInstance();
            if (cm.ContainsKey(key))
            {
                return cm[key];
            }
            else
            {
                string sql = @" select  COLUMN_name 
                            from user_tab_columns c  
                            where c.Table_Name='" + tableName + @"' 
                            order by c.column_name";
                var reval = db.SqlQuery<string>(sql);
                cm.Add(key, reval, cm.Day);
                return reval;
            }
        }

        /// <summary>
        ///tableOrView  null=u,v , true=u , false=v
        /// </summary>
        /// <param name="tableOrView"></param>
        /// <returns></returns>
        internal static string GetCreateClassSql(bool? tableOrView)
        {
            string sql = null;
            if (tableOrView == null)
            {
                sql = @"
                select  table_name name from user_tables where
                         table_name!='HELP' 
                        AND table_name NOT LIKE '%$%'
                        AND table_name NOT LIKE 'LOGMNRC_%'
                        AND table_name!='LOGMNRP_CTAS_PART_MAP'
                        AND table_name!='LOGMNR_LOGMNR_BUILDLOG'
                        AND table_name!='SQLPLUS_PRODUCT_PROFILE'  
                        UNION all
                        select view_name name  from user_views 
                                                WHERE VIEW_name NOT LIKE '%$%'
                                                AND VIEW_NAME !='PRODUCT_PRIVS'
                        AND VIEW_NAME NOT LIKE 'MVIEW_%'  ";
            }
            else if (tableOrView == true)
            {
                sql = @"select  table_name name from user_tables where
                         table_name!='HELP' 
                        AND table_name NOT LIKE '%$%'
                        AND table_name NOT LIKE 'LOGMNRC_%'
                        AND table_name!='LOGMNRP_CTAS_PART_MAP'
                        AND table_name!='LOGMNR_LOGMNR_BUILDLOG'
                        AND table_name!='SQLPLUS_PRODUCT_PROFILE' ";
            }
            else
            {
                sql = @"select view_name name  from user_views 
                        WHERE VIEW_name NOT LIKE '%$%'
                        AND VIEW_NAME !='PRODUCT_PRIVS'
                        AND VIEW_NAME NOT LIKE 'MVIEW_%' ";
            }
            return sql;
        }

        internal static string GetTtableColumnsInfo(string tableName)
        {
            string sql = @"SELECT  Sysobjects.name AS TABLE_NAME ,
								syscolumns.Id  AS TABLE_ID,
								syscolumns.name AS COLUMN_NAME ,
								systypes.name AS DATA_TYPE ,
								syscolumns.length AS CHARACTER_MAXIMUM_LENGTH ,
								sys.extended_properties.[value] AS COLUMN_DESCRIPTION ,
								syscomments.text AS COLUMN_DEFAULT ,
								syscolumns.isnullable AS IS_NULLABLE,
                                (case when exists(SELECT 1 FROM sysobjects where xtype= 'PK' and name in ( 
                                SELECT name FROM sysindexes WHERE indid in( 
                                SELECT indid FROM sysindexkeys WHERE id = syscolumns.id AND colid=syscolumns.colid 
                                ))) then 1 else 0 end) as IS_PRIMARYKEY

								FROM    syscolumns
								INNER JOIN systypes ON syscolumns.xtype = systypes.xtype
								LEFT JOIN sysobjects ON syscolumns.id = sysobjects.id
								LEFT OUTER JOIN sys.extended_properties ON ( sys.extended_properties.minor_id = syscolumns.colid
																			 AND sys.extended_properties.major_id = syscolumns.id
																		   )
								LEFT OUTER JOIN syscomments ON syscolumns.cdefault = syscomments.id
								WHERE   syscolumns.id IN ( SELECT   id
												   FROM     SYSOBJECTS
												   WHERE    xtype in( 'U','V') )
								AND ( systypes.name <> 'sysname' ) AND Sysobjects.name='" + tableName + "'  AND systypes.name<>'geometry' AND systypes.name<>'geography'  ORDER BY syscolumns.colid";
            return sql;
        }

        internal static string GetSelectTopSql()
        {
            return "select top 1 * from {0}";
        }

        /// <summary>
        /// 将SqlType转成C#Type
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static string ChangeDBTypeToCSharpType(string typeName)
        {
            string reval = string.Empty;
            switch (typeName.ToLower())
            {
                case "long":
                    throw new Exception("不支持Oracle的Long类型，建议使用C_LOB代替。");
                case "int16":
                case "int32":
                case "int":
                    reval = "int";
                    break;
                case "text":
                    reval = "string";
                    break;
                case "int64":
                    reval = "long";
                    break;
                case "binary":
                    reval = "object";
                    break;
                case "bit":
                    reval = "bool";
                    break;
                case "char":
                    reval = "string";
                    break;
                case "datetime":
                case "date":
                    reval = "dateTime";
                    break;
                case "decimal":
                    reval = "decimal";
                    break;
                case "float":
                case "binarydouble":
                case "double":
                    reval = "double";
                    break;
                case "image":
                    reval = "byte[]";
                    break;
                case "money":
                    reval = "decimal";
                    break;
                case "nchar":
                    reval = "string";
                    break;
                case "ntext":
                    reval = "string";
                    break;
                case "numeric":
                    reval = "decimal";
                    break;
                case "nvarchar":
                    reval = "string";
                    break;
                case "real":
                    reval = "float";
                    break;
                case "smalldatetime":
                    reval = "dateTime";
                    break;
                case "smallint":
                    reval = "short";
                    break;
                case "smallmoney":
                    reval = "decimal";
                    break;
                case "timestamp":
                    reval = "dateTime";
                    break;
                case "tinyint":
                    reval = "byte";
                    break;
                case "uniqueidentifier":
                    reval = "guid";
                    break;
                case "varbinary":
                case "blob":
                case "long raw":
                case "raw":
                case "bfile":
                    reval = "byte[]";
                    break;
                case "varchar":
                    reval = "string";
                    break;
                case "Variant":
                    reval = "object";
                    break;
                default:
                    reval = "string";
                    break;
            }
            return reval;
        }

        /// <summary>
        /// par的符号
        /// </summary>
        public const char ParSymbol = '@';

        /// <summary>
        /// 获取转释后的表名和列名
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static string GetTranslationSqlName(string name)
        {
            Check.ArgumentNullException(name, "表名不能为空。");
            var hasScheme = name.Contains(".");
            if (name.Contains("[")) return name;
            if (hasScheme)
            {
                var array = name.Split('.');
                if (array.Length == 2)
                {
                    return string.Format("[{0}].[{1}]", array.First(), array.Last());
                }
                else
                {
                    return string.Join(".", array.Select(it => "[" + it + "]"));
                }
            }
            else
            {
                return "[" + name + "]";
            }
        }
        /// <summary>
        /// 获取参数名
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static string GetOracleParameterName(string name)
        {
            return ParSymbol + name;
        }

        /// <summary>
        ///获取没有符号的参数名称
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static string GetOracleParameterNameNoParSymbol(string name)
        {
            return name.TrimStart(ParSymbol);
        }

        /// <summary>
        /// 获取Schema和表名的集合
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        internal static List<KeyValue> GetSchemaList(SqlSugarClient db)
        {
            return new List<KeyValue>();
        }
    }
}
