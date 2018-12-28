using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Kesco.App.Web.Docs.Vacation
{
    /// <summary>
    /// Вспомогательный класс для выполнения скриптов на стороне клиента
    /// </summary>
    /// 
    public static class VacationClientScripts
    {
        //Метод для установки глобальных переменных для функций из Holyday.js
        public static void InitializeGlobalVariables(Kesco.Lib.Web.Controls.V4.Common.Page p)
        {
            p.JS.Write("StrResources = {{{0}}}; ", p.Resx.GetString("Vacation_JsResources"));
        }

        //Метод отправляет в клиентское приложение данные о созданных замещениях в формате XML
        public static void SetSubs(Kesco.Lib.Web.Controls.V4.Common.Page p, string str_table)
        {
            p.JS.Write("displaySubTable('{0}');", HttpUtility.JavaScriptStringEncode(str_table));
        }

        //Метод открывает диалог редактирования замещения
        public static void DisplaySub(Kesco.Lib.Web.Controls.V4.Common.Page p, int sub_id, bool read_only, string subTitle)
        {
            p.JS.Write("displaySub({0}, {1}, \"{2}\");", sub_id, read_only ? "true" : "false", subTitle);
        }

        //Метод закрывает диалог редактирования замещения
        public static void CloseSub(Kesco.Lib.Web.Controls.V4.Common.Page p)
        {
            p.JS.Write("closeSubDialog();");
        }
    }
}