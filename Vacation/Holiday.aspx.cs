using System;
using System.Linq;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using Kesco.Lib.DALC;
using Kesco.Lib.Entities.Documents;
using Kesco.Lib.Entities.Persons;
using Kesco.Lib.Entities.Corporate;
using Kesco.Lib.Entities.Documents.EF.Applications;
using Kesco.Lib.Web.Settings;
using Kesco.Lib.Web.Controls.V4;
using Kesco.Lib.Web.Controls.V4.Common.DocumentPage;
using Kesco.Lib.BaseExtention.Enums.Corporate;
using Kesco.Lib.BaseExtention.Enums.Controls;
using Kesco.Lib.Log;
using Kesco.Lib.Web.DBSelect.V4;

namespace Kesco.App.Web.Docs.Vacation
{
	//Класс страницы для создания заявления на отпуск
	public partial class Holiday : DocPage
	{
		//Объект для синхронизации доступа к сохраненнным в кэше замещениям
		private static object syncCacheSubs = new object();

		//Дополнение к идентификатору документа для создания ключа в Chache
		private const string CacheSubs = "_subs";

		//Класс описывающий замещения сотрудников
		class Substitution
		{
			public static int last_sub_id = 1;//Начальное значение идентификатора замещения в приложении

			public int id;//Идентификатор замещения в приложении
			public int code;//Идентификатор замещения в БД
			public DateTime from;//Дата начала действия замещения
			public DateTime to;//Дата конца действия замещения
			public int person_id;//Идентификатор замещающего сотрудника
			public string person;//Имя замещающего сотрудника
			public string description;//Причина замещения

			/// <summary>
			/// Метод для создания глубокой копии объекта, для сохранения или получения объекта из кэша приложения
			/// </summary>
			/// <returns>Копия объекта</returns>
			public Substitution Clone()
			{
				return (Substitution)MemberwiseClone();
			}
		}

		//Код территории по умолчанию
		private const int defTerritory = 188;
		//Сотрудник, от имени которого составляется заявление
		private Employee _empl;
		//Компания, в которой работает сотрудник, от имени которого составляется заявление
		private Person _company;
		//Список замещений сотрудников связанных с документом
		private List<Substitution> _subs = new List<Substitution>();
		//Формат даты по умолчанию
		private string _date_format = "dd.MM.yyyy";

		public Holiday()
		{
			_empl = CurrentUser;
			if (_empl.Employer == null) return;
			_company = new Person(_empl.Employer.Id);
		}

		protected override void ProcessCommand(string cmd, NameValueCollection param)
		{
			switch (cmd)
			{
				case "save_sub":
					{
						int sub_id = -1;
						if (!int.TryParse(param["sub_id"], out sub_id)) break;
						SaveSub(sub_id);
					}
					break;

				case "del_sub":
					{
						int sub_id = 0;
						if (!int.TryParse(param["sub_id"], out sub_id)) break;
						DeleteSub(sub_id);

						string close_dlg = param["close_dlg"];
						if (close_dlg == "true")
							VacationClientScripts.CloseSub(this);
					}
					break;

				case "edit_sub":
					{
						int sub_id = 0;
						if (!int.TryParse(param["sub_id"], out sub_id)) break;
						EditSub(sub_id);
					}
					break;

				case "close_sub":
					//Продолжаем сохранение
					ResolveSubErrorrs(true);
					break;

				default:
					base.ProcessCommand(cmd, param);
					break;
			}
		}

		protected override void OnDocumentSaved()
		{
			if (!selSubForm.Visible) return;

			ResolveSubErrorrs(false);

			List<Substitution> to_keep_subs = _subs.Where(sub => sub.code == 0).ToList();

			//Сохранение замещений в кэше приложения
			if (to_keep_subs.Count > 0)
			{
				lock (syncCacheSubs)
				{
					List<Substitution> copied_subs = new List<Substitution>();

					//Копирование замещений
					foreach (Substitution s in to_keep_subs)
					{
						copied_subs.Add(s.Clone());
					}

					Cache.Insert(Doc.Id + CacheSubs, copied_subs, null, System.Web.Caching.Cache.NoAbsoluteExpiration, TimeSpan.FromSeconds(300));
				}
			}
		}

		protected override void OnDocDateChanged(object sender, ProperyChangedEventArgs e)
		{
			base.OnDocDateChanged(sender, e);
			dateFrom.RenderNtf();
		}

		protected override void DocumentInitialization(Document copy = null)
		{
			if (copy == null)
				Doc = new Lib.Entities.Documents.EF.Applications.Vacation(_empl);
			else
			{
				Doc = (Lib.Entities.Documents.EF.Applications.Vacation)copy;
			}

			Doc.Date = DateTime.Today;
		}

		protected override void DocumentToControls()
		{
			Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;

			if (_empl.EmployeeId != vd.EmployeeFrom.ValueInt)
				_empl = (Employee)selPerson.GetObjectById(vd.EmployeeFrom.ValueString);

			//selPerson.Value = ((int)vd.EmployeeFrom.Value).ToString();
			//selCompany.Value = ((int)vd.CompanyFrom.Value).ToString();

			if (null == vd.EmployeeFrom.Value)
				selPerson.Value = null;
			else
				selPerson.Value = ((int)vd.EmployeeFrom.Value).ToString();

			if (null == vd.CompanyFrom.Value)
				selCompany.Value = null;
			else
				selCompany.Value = ((int)vd.CompanyFrom.Value).ToString();

			if (null == vd.EmployeeTo.Value)
				selDirector.Value = null;
			else
				selDirector.Value = ((int)vd.EmployeeTo.Value).ToString();

			if (null != vd.VacationType.Value)
				cbType.Value = ((int)vd.VacationType.Value).ToString();

			if (null != vd.DateFrom.Value)
				dateFrom.ValueDate = (DateTime)vd.DateFrom.Value;

			if (null != vd.Days.Value)
				numDays.ValueInt = (int)vd.Days.Value;

			if (null != vd.DateTo.Value)
				dateTo.ValueDate = (DateTime)vd.DateTo.Value;

			selDirector.WeakList = GetDirectors();
			if (null != selDirector.WeakList && selDirector.WeakList.Count == 1)
			{
				vd.EmployeeTo.Value = selDirector.WeakList[0];
				//selDirector.Value = selDirector.WeakList[0];
			}
		}

		protected override void SetControlProperties()
		{
			Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;

			selPerson.IsRequired = vd.EmployeeFrom.IsMandatory;
			selCompany.IsRequired = vd.CompanyFrom.IsMandatory;
			selDirector.IsRequired = vd.EmployeeTo.IsMandatory;
			cbType.IsRequired = vd.VacationType.IsMandatory;
			dateFrom.IsRequired = vd.DateFrom.IsMandatory;
			numDays.IsRequired = vd.Days.IsMandatory;
			dateTo.IsRequired = vd.DateTo.IsMandatory;

			if (!DocEditable)
			{
				selPerson.IsReadOnly = true;
				selCompany.IsReadOnly = true;
				selDirector.IsReadOnly = true;
				cbType.IsReadOnly = true;

				dateFrom.IsReadOnly = true;
				numDays.IsReadOnly = true;
				dateTo.IsReadOnly = true;
			}

			selPerson.IsRequired = vd.EmployeeFrom.IsMandatory;
			selCompany.IsRequired = vd.CompanyFrom.IsMandatory;
			selDirector.IsRequired = vd.EmployeeTo.IsMandatory;
			cbType.IsRequired = vd.VacationType.IsMandatory;
			dateFrom.IsRequired = vd.DateFrom.IsMandatory;
			numDays.IsRequired = vd.Days.IsMandatory;
			dateTo.IsRequired = vd.DateTo.IsMandatory;

			selPerson.BindDocField = vd.EmployeeFrom;
			selCompany.BindDocField = vd.CompanyFrom;
			selDirector.BindDocField = vd.EmployeeTo;

			cbType.BindDocField = vd.VacationType;
			dateFrom.BindDocField = vd.DateFrom;
			numDays.BindDocField = vd.Days;
			dateTo.BindDocField = vd.DateTo;
		}

		protected void Page_Load(object sender, EventArgs e)
		{
			VacationClientScripts.InitializeGlobalVariables(this);

			btnAddSubForm.Alt = Resx.GetString("Vacation_AddSubFormAlt");
			btnAddSubForm.Attributes["title"] = Resx.GetString("Vacation_AddSubFormTitle");

			if (Doc.IsNew)
			{
				DocumentToControls();
			}
			else
			{
				lock (syncCacheSubs)
				{
					List<Substitution> old_subs = (List<Substitution>)Cache[Doc.Id + CacheSubs];

					//Загрузка и копирование замещений из старой страницы
					if (null != old_subs)
					{
						foreach (Substitution s in old_subs)
						{
							_subs.Add(s.Clone());
						}

						ResolveSubErrorrs(true);
					}
					else
					{
						LoadSubs();
						SendSubs();
					}
				}
			}

			numDays.MinValue = 1;
			numDays.MaxValue = 1095;//Неизвестно почему, наверное 365*3

			if (!DocEditable || IsPrintVersion)
			{
				//Для таких состояний документов снимаем ограничения на состояние сотрудника
				selSub.Filter.Status.ValueStatus = СотоянияСотрудника.Неважно;
				selPerson.Filter.Status.ValueStatus = СотоянияСотрудника.Неважно;
			}
			else
			{
				selPerson.Filter.Status.ValueStatus = СотоянияСотрудника.Работающие;
				selSub.Filter.Status.ValueStatus = СотоянияСотрудника.Работающие;
			}

			selCompany.Filter.PersonType = 1;
			selDirector.Filter.PersonType = 2;

			if (dateTo.ValueDate.HasValue)
			{
				DateTime dFirstWorkingDay = GetFirstWorkingDay(dateTo.ValueDate.GetValueOrDefault().AddDays(1));
				dateFirst.Value = dFirstWorkingDay.ToString(_date_format);
			}

			selPerson.OnRenderNtf += new RenderNtfDelegate(selPerson_OnRenderNtf);
			selCompany.OnRenderNtf += new RenderNtfDelegate(selCompany_OnRenderNtf);
			selDirector.OnRenderNtf += new RenderNtfDelegate(selDirector_OnRenderNtf);

			dateFrom.OnRenderNtf += new RenderNtfDelegate(dateFrom_OnRenderNtf);
			dateTo.OnRenderNtf += new RenderNtfDelegate(dateTo_OnRenderNtf);
			numDays.OnRenderNtf += new RenderNtfDelegate(numDays_OnRenderNtf);

			dateFrom.Changed += new ChangedEventHandler(dateFrom_Changed);
			dateTo.Changed += new ChangedEventHandler(dateTo_Changed);

			ShowHideSubs();

			SetFocusToFirstEmptyField();
		}

		protected void cbType_BeforeSearch(object sender)
		{
			if (null != _empl && _empl.Gender == "Ж")//Отпуск по беременности и родам доступен только для женщин, значение "М" может отсутствовать у некоторых записей в БД
			{
				cbType.Filter.VacationTypeId.Clear();
			}
			else
			{
				cbType.Filter.VacationTypeId.HowSearch = "1";
				cbType.Filter.VacationTypeId.Set("4");
			}
		}

		protected void cbType_Changed(object sender, ProperyChangedEventArgs e)
		{
		}

		void numDays_OnRenderNtf(object sender, Ntf ntf)
		{
			dateFrom.RenderNtf();
		}

		void dateTo_OnRenderNtf(object sender, Ntf ntf)
		{
			ntf.Clear();

			if (!dateTo.ValueDate.HasValue) return;

			DateTime to = dateTo.ValueDate.GetValueOrDefault(DateTime.MaxValue);

			if (to < DateTime.Today)
				ntf.Add(Resx.GetString("Vacation_NtfDateInPast"), NtfStatus.Information);

			if (to < Doc.Date)
				ntf.Add(Resx.GetString("Vacation_NtfDateLessThatDateDoc"), NtfStatus.Error);

			if (!dateFrom.ValueDate.HasValue) return;

			DateTime from = dateFrom.ValueDate.GetValueOrDefault(DateTime.MinValue);

			if (to < from)
				ntf.Add(Resx.GetString("Vacation_NtfDatesAreNotValid"), NtfStatus.Error);
		}

		void dateFrom_OnRenderNtf(object sender, Ntf ntf)
		{
			ntf.Clear();

			if (!dateFrom.ValueDate.HasValue) return;

			DateTime from = dateFrom.ValueDate.GetValueOrDefault(DateTime.MinValue);

			if (from < DateTime.Today)
				ntf.Add(Resx.GetString("Vacation_NtfDateInPast"), NtfStatus.Information);

			if (from < Doc.Date)
				ntf.Add(Resx.GetString("Vacation_NtfDateLessThatDateDoc"), NtfStatus.Error);

			dateTo.RenderNtf();
		}

		void selDirector_OnRenderNtf(object sender, Ntf ntf)
		{
			ntf.Clear();

			if (!selDirector.ValueInt.HasValue) return;

			Person director = selDirector.GetObjectById(selDirector.Value) as Person;
			if (director.Unavailable) ntf.Add(Resx.GetString("Vacation_NtfPersonIsNotAvailable"), NtfStatus.Error);
			else
			{
				if (director.EndDate > Doc.Date)
					ntf.Add(Resx.GetString("Vacation_NtfNotExistsOnDocDate"), NtfStatus.Error);

				if (_company != null && !_company.Unavailable)
				{
					//Dictionary<string, object> args = new Dictionary<string, object>();
					//args.Add("@КодЛицаКто", director.Id);
					//args.Add("@КодЛицаГде", _company.Id);
					//object test_obj = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.SELECT_ТестМестоРаботыЛица, CommandType.Text, Config.DS_user, args);

					//if (test_obj == null) ntf.Add(Resx.GetString("Vacation_NtfWorkStaff"), NtfStatus.Error);

					List<string> listOfDirectors = GetDirectors();
					if (!listOfDirectors.Contains(selDirector.Value))
						ntf.Add(Resx.GetString("Vacation_NtfNoFirstSign"), NtfStatus.Error);
				}
			}
		}

		void selCompany_OnRenderNtf(object sender, Ntf ntf)
		{
			ntf.Clear();

			if (!selCompany.ValueInt.HasValue) return;
			if (_company == null) return;
			if (_company.Unavailable) ntf.Add(Resx.GetString("Vacation_NtfPersonIsNotAvailable"), NtfStatus.Error);
			else
			{
				TestEmplCompanyNtf(ntf);

				if (_company.EndDate > Doc.Date)
					ntf.Add(Resx.GetString("Vacation_NtfNotExistsOnDocDate"), NtfStatus.Error);
			}
		}

		void selPerson_OnRenderNtf(object sender, Ntf ntf)
		{
			ntf.Clear();

			if (!selPerson.ValueInt.HasValue) return;

			if (_empl.Unavailable) ntf.Add(Resx.GetString("Vacation_NtfPersonIsNotAvailable"), NtfStatus.Error);
			else
			{
				TestEmplCompanyNtf(ntf);

				if (_empl.PersonEmployee.EndDate > Doc.Date)
					ntf.Add(Resx.GetString("Vacation_NtfNotExistsOnDocDate"), NtfStatus.Error);
			}
		}

		protected void selPerson_BeforeSearch(object sender)
		{
			//Место работы заявителя совпадает с указанным
			int person_id = 0;
			if (null != selCompany.Value && int.TryParse(selCompany.Value, out person_id))
			{
				selPerson.Filter.IdsCompany.CompanyHowSearch = "0";//IN
				selPerson.Filter.IdsCompany.Value = selCompany.Value;
			}
			else
			{
				selPerson.Filter.IdsCompany.Clear();
			}
		}

		protected void selSub_BeforeSearch(object sender)
		{
			DBSEmployee emplCtrl = sender as DBSEmployee;
			emplCtrl.Filter.Status.ValueStatus = СотоянияСотрудника.Работающие;
			emplCtrl.Filter.HasLogin.ValueHasLogin = НаличиеЛогина.ЕстьЛогин;
			//Заместитель является работником организации
			/*
			int person_id = 0;
			if (null != selCompany.Value && int.TryParse(selCompany.Value, out person_id))
			{
				selSub.Filter.IdsCompany.CompanyHowSearch="0";//IN
				selSub.Filter.IdsCompany.Value = selCompany.Value;
			}
			else
			{
				selSub.Filter.IdsCompany.Clear();
			}
			*/
			//Его реквизиты должны быть действительны на дату начала отпуска
		}

		protected void selDirector_BeforeSearch(object sender)
		{
			//Место работы руководителя совпадает с указанным
			int person_id = 0;
			if (null != selCompany.Value && int.TryParse(selCompany.Value, out person_id))
			{
				selDirector.Filter.PersonLink = person_id;
				selDirector.Filter.PersonLinkType = 3;
				selDirector.Filter.PersonSignType = 1;
			}
			else
			{
				selDirector.Filter.PersonLink = null;
				selDirector.Filter.PersonLinkType = null;
			}

			//Его реквизиты действительны и он является руководителем на дату документа
			if (default(DateTime) == Doc.Date)
			{
				selDirector.Filter.PersonValidAt = null;
				selDirector.Filter.PersonLinkValidAt = null;
			}
			else
			{
				selDirector.Filter.PersonValidAt = Doc.Date;
				selDirector.Filter.PersonLinkValidAt = Doc.Date;
			}
		}

		protected void selCompany_BeforeSearch(object sender)
		{
			//Организация является местом работы заявителя или руководителя
			int person_id = 0;
			if (null != _empl && (person_id = _empl.PersonEmployeeId.GetValueOrDefault()) != 0
				|| null != selDirector.Value && int.TryParse(selDirector.Value, out person_id))
			{
				selCompany.Filter.PersonLink = person_id;
				selCompany.Filter.PersonLinkType = 2;
			}
			else
			{
				selCompany.Filter.PersonLink = null;
				selCompany.Filter.PersonLinkType = null;
			}

			//Её реквизиты действительны на дату документа и она является местом работы на дату документа
			if (default(DateTime) == Doc.Date)
			{
				selCompany.Filter.PersonValidAt = null;
				selCompany.Filter.PersonLinkValidAt = null;
			}
			else
			{
				selCompany.Filter.PersonValidAt = Doc.Date;
				selCompany.Filter.PersonLinkValidAt = Doc.Date;
			}
		}

		protected void selPerson_Changed(object sender, Kesco.Lib.Web.Controls.V4.ProperyChangedEventArgs e)
		{
			_empl = (Employee)selPerson.GetObjectById(selPerson.Value);

			Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;

			if (null == _empl)
				vd.PersonFrom.Value = null;
			else
				vd.PersonFrom.Value = _empl.PersonEmployeeId;

			TestEmplInfoFilled();
		}

		protected void selCompany_Changed(object sender, Kesco.Lib.Web.Controls.V4.ProperyChangedEventArgs e)
		{
			_company = (Person)selCompany.GetObjectById(selCompany.Value);

			selDirector.WeakList = GetDirectors();
			if (null != selDirector.WeakList && selDirector.WeakList.Count == 1)
			{
				Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;
				vd.EmployeeTo.Value = selDirector.WeakList[0];
				//selDirector.Value = selDirector.WeakList[0];
			}

			//Изменяется код территории
			SetDaysAndDates(null);

			TestEmplInfoFilled();
		}

		protected void selDirector_Changed(object sender, Kesco.Lib.Web.Controls.V4.ProperyChangedEventArgs e)
		{
			TestEmplInfoFilled();
		}

		void dateTo_Changed(object sender, ProperyChangedEventArgs e)
		{
			if (dateTo.ValueDate.GetValueOrDefault() < dateFrom.ValueDate.GetValueOrDefault())
			{
				Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;
				vd.DateFrom.Value = vd.DateTo.Value;
				vd.Days.Value = 1;
			}

			//Свойства MaxDate и MinDate работают некорректно, они изменяют значение ValueDate без отправки уведомлений OnChange
			//dateFrom.MaxDate = dateTo.Value;

			ShowHideSubs();
		}

		void dateFrom_Changed(object sender, ProperyChangedEventArgs e)
		{
			if (!dateTo.ValueDate.HasValue) return;

			if (dateTo.ValueDate.GetValueOrDefault() < dateFrom.ValueDate.GetValueOrDefault())
			{
				Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;
				vd.DateTo.Value = vd.DateFrom.Value;
				vd.Days.Value = 1;
			}

			//Свойства MaxDate и MinDate работают некорректно, они изменяют значение ValueDate без отправки уведомлений OnChange
			//dateTo.MinDate = dateFrom.Value;

			ShowHideSubs();
		}

		protected void DaysOrDates_Changed(object sender, Kesco.Lib.Web.Controls.V4.ProperyChangedEventArgs e)
		{
			SetDaysAndDates(sender);
			ShowHideSubs();
		}

		//Проверка, того что лицо является сотрудником компании, отображение уведомления
		void TestEmplCompanyNtf(Ntf ntf)
		{
			if (_company == null) return;

			Dictionary<string, object> args = new Dictionary<string, object>();
			args.Add("@КодЛица", _company.Id);
			args.Add("@КодСотрудника", _empl.EmployeeId);
			object test_obj = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.SELECT_ТестМестоРаботыСотрудника, CommandType.Text, Config.DS_user, args);

			if (test_obj == DBNull.Value) ntf.Add(Resx.GetString("Vacation_NtfWorkStaff"), NtfStatus.Error);
		}

		//Загрузка замещений по документу из БД
		void LoadSubs()
		{
			Dictionary<string, object> args = new Dictionary<string, object>();
			args.Add("@КодДокумента", Doc.Id);
			DataTable dt = DBManager.GetData(Kesco.Lib.Entities.SQLQueries.SELECT_ЗамещенияПоДокументу, Config.DS_user, CommandType.Text, args);

			if (null != dt)
			{
				Substitution.last_sub_id = 1;

				_subs = dt.AsEnumerable().Select(row => new Substitution()
				{
					id = Substitution.last_sub_id++,
					code = row.Field<int>("КодЗамещенияСотрудников"),
					from = row.Field<DateTime>("От"),
					to =row.Field<DateTime>("До"),
					person_id = row.Field<int>("КодСотрудникаЗамещающего"),
					person = row.Field<string>("Сотрудник"),
					description = row.Field<string>("Примечания")
				}).ToList();
			}
		}

		//Отправка замещений сотрудников в клиентское приложение
		private void SendSubs()
		{
			XElement xmlSubs = new XElement("subs", _subs.OrderBy(sub => sub.person).Select(sub =>
			{

				DateTime sub_to = sub.to > DateTime.MinValue ? sub.to.AddDays(-1) : sub.to;
				int disabled = sub.code != 0 && sub_to <= DateTime.Today ? 1 : 0;
				string sub_description = string.Empty;
				if (sub.from != dateFrom.ValueDate || sub_to != dateTo.ValueDate)
				{
					sub_description = string.Format(Resx.GetString("Vacation_SubDates"), sub.from.ToString(_date_format), sub_to.ToString(_date_format));
				}

				return new XElement("sub",
					new XAttribute("id", sub.id),
					new XAttribute("person", sub.person),
					new XAttribute("description", sub_description),
					new XAttribute("disabled", disabled));
			}
												));

			VacationClientScripts.SetSubs(this, xmlSubs.ToString());
		}

		/// <summary>
		/// Отображение ошибки создания или изменения замещения
		/// </summary>
		/// <param name="err_msg">Сообщение об ошибке</param>
		/// <param name="fDialog">Если true, то ошибка отображается в диалоге редактирования замещения, иначе в отдельном MessageBox</param>
		void DisplaySubError(string err_msg, bool fDialog)
		{
			if (fDialog)
			{
				SubErrMsg.Value = err_msg;
			}
			else
			{
				ShowMessage(err_msg, Resx.GetString("Vacation_ErrSubTitle"));
			}
		}

		/// <summary>
		/// Сохранение замещения
		/// </summary>
		/// <param name="sub_id">Идентификатор замещения в приложении</param>
		void SaveSub(int sub_id)
		{
			DisplaySubError(string.Empty, true);

			if (sub_id < 1)
			{
				if (!dateFrom.ValueDate.HasValue || !dateTo.ValueDate.HasValue)
				{
					DisplaySubError(Resx.GetString("Vacation_ErrSubDocDates"), sub_id >= 0);
					return;
				}

				//Добавление замещения из формы заявления на отпуск
				//Замещение создается не ранее, чем с завтрашнего дня, что бы была возможность его удалить
				//затем можно отредактировать замещение и начать его с сегодняшнего дня
				dateSubFrom.ValueDate = dateFrom.ValueDate;
				dateSubTo.ValueDate = dateTo.ValueDate;

				DateTime nextDay = DateTime.Today.AddDays(1);
				if (dateSubFrom.ValueDate.GetValueOrDefault() < nextDay && dateSubTo.ValueDate.GetValueOrDefault() >= nextDay)
				{
					dateSubFrom.ValueDate = nextDay;
				}

				//Если дата окнчания в прошлом или сегодня, то создается замещение на один день после начала
				if (dateSubTo.ValueDate.GetValueOrDefault() < dateSubFrom.ValueDate.GetValueOrDefault()) dateSubTo.ValueDate = dateSubFrom.ValueDate;

				//Не пытатемся создавать замещения длинее 30 дней
				DateTime next30Day = dateSubFrom.ValueDate.GetValueOrDefault().AddDays(30 - 1);
				if (dateSubTo.ValueDate.GetValueOrDefault() > next30Day) dateSubTo.ValueDate = next30Day;

				selSub.ValueInt = selSubForm.ValueInt;

				if (Doc.IsNew)
				{
					textDescription.Value = cbType.ValueText;
					if (textDescription.Value.Length > 0)
						textDescription.Value += " (" + string.Format(Resx.GetString("Vacation_SubDocFrom"), Doc.Date.ToString(_date_format)) + ")";
					else
						textDescription.Value = string.Format(Resx.GetString("Vacation_SubDocFrom"), Doc.Date.ToString(_date_format));
				}
				else
					textDescription.Value = cbType.ValueText + " (" + string.Format(Resx.GetString("Vacation_SubDoc"), Doc.Id, Doc.Date.ToString(_date_format)) + ")";
			}

			if (selSub.Value.Length == 0)
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubEmployee"), sub_id >= 0);
				return;
			}

			if (!dateSubFrom.ValueDate.HasValue)
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubFrom"), sub_id >= 0);
				return;
			}

			if (!dateSubTo.ValueDate.HasValue)
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubTo"), sub_id >= 0);
				return;
			}

			if (dateSubFrom.ValueDate > dateSubTo.ValueDate)
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubDates"), sub_id >= 0);
				return;
			}

			if (dateSubFrom.ValueDate.GetValueOrDefault().AddDays(30) <= dateSubTo.ValueDate.GetValueOrDefault())
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubDates30"), sub_id >= 0);
				return;
			}

			Substitution sub = null;

			if (sub_id > 0)
				sub = _subs.Find(s => s.id == sub_id);

			bool fAddNew = null == sub;
			if (fAddNew)
			{
				sub = new Substitution
				{
					id = Substitution.last_sub_id++,
					from = dateSubFrom.ValueDate.GetValueOrDefault(),
					to = dateSubTo.ValueDate.GetValueOrDefault(),
					person = selSub.ValueText,
					person_id = selSub.ValueInt.GetValueOrDefault(),
					description = textDescription.Value
				};

				if (sub.to < DateTime.MaxValue) sub.to = sub.to.AddDays(1);
			}

			string new_subperson = selSub.ValueText;
			int new_personid = selSub.ValueInt.GetValueOrDefault();
			DateTime new_sub_from = dateSubFrom.ValueDate.GetValueOrDefault();
			DateTime new_sub_to = dateSubTo.ValueDate.GetValueOrDefault();
			if (new_sub_to < DateTime.MaxValue) new_sub_to = new_sub_to.AddDays(1);
			string new_subdescription = textDescription.Value;

			try
			{
				if (!Doc.IsNew)
				{
					if (sub.code == 0)
					{
						Dictionary<string, object> ins_args = new Dictionary<string, object>();
						ins_args["@От"] = new_sub_from;
						ins_args["@До"] = new_sub_to;
						ins_args["@КодСотрудникаЗамещаемого"] = _empl.EmployeeId;
						ins_args["@КодСотрудникаЗамещающего"] = new_personid;
						ins_args["@Примечания"] = new_subdescription;
						ins_args["@КодДокумента"] = Doc.Id/*EntityId*/;

						object obj_code = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.INSERT_Замещения, CommandType.Text, Config.DS_user, ins_args);
						if (obj_code != DBNull.Value)
							sub.code = Decimal.ToInt32((Decimal)obj_code);
					}
					else
					{
						Dictionary<string, object> upd_args = new Dictionary<string, object>();
						upd_args["@От"] = new_sub_from;
						upd_args["@До"] = new_sub_to;
						upd_args["@КодСотрудникаЗамещающего"] = new_personid;
						upd_args["@Примечания"] = new_subdescription;
						upd_args["@КодДокумента"] = Doc.Id/*EntityId*/;
						upd_args["@КодЗамещенияСотрудников"] = sub.code;

						DBManager.ExecuteNonQuery(Kesco.Lib.Entities.SQLQueries.UPDATE_Замещения, CommandType.Text, Config.DS_user, upd_args);
					}
				}

				sub.person = new_subperson;
				sub.person_id = new_personid;
				sub.from = new_sub_from;
				sub.to = new_sub_to;
				sub.description = new_subdescription;

				if (fAddNew)
				{
					_subs.Add(sub);
					selSubForm.Value = null;
				}

				SendSubs();
				VacationClientScripts.CloseSub(this);
			}
			catch (DetailedException dex)
			{
				DisplaySubError(Resx.GetString("Vacation_ErrSubDb") + "<br/>" + dex.Message, sub_id >= 0);
			}
		}

		/// <summary>
		/// Удаление замешения
		/// </summary>
		/// <param name="sub_id">Идентификатор замещения в приложении</param>
		void DeleteSub(int sub_id)
		{
			if (sub_id < 1) return;

			Substitution sub = _subs.Find(s => s.id == sub_id);
			if (null == sub) return;

			try
			{
				if (sub.code == 0)
				{
					_subs.RemoveAll(s => s.id == sub.id);
				}
				else
				{
					Dictionary<string, object> del_args = new Dictionary<string, object>();
					del_args["@КодЗамещенияСотрудников"] = sub.code;

					DBManager.ExecuteNonQuery(Kesco.Lib.Entities.SQLQueries.DELETE_Замещения, CommandType.Text, Config.DS_user, del_args);

					LoadSubs();
				}

				SendSubs();
			}
			catch (DetailedException dex)
			{
				ShowMessage(Resx.GetString("Vacation_ErrSubDb") + Environment.NewLine + dex.Message, Resx.GetString("Vacation_ErrSubTitle"), MessageStatus.Error);
			}
		}

		/// <summary>
		/// Редактирование замещения
		/// </summary>
		/// <param name="sub_id">Идентификатор замещения в приложении</param>
		void EditSub(int sub_id)
		{
			DisplaySubError(string.Empty, true);

			Substitution sub = _subs.Find(s => s.id == sub_id);
			if (null == sub) return;
			EditSub(sub);
		}

		/// <summary>
		/// Редактирование замещения
		/// </summary>
		/// <param name="sub">Идентификатор замещения в приложении</param>
		void EditSub(Substitution sub)
		{
			object obj_today = DBManager.ExecuteScalar("SELECT CAST(GETUTCDATE() as date)", CommandType.Text, Config.DS_user, null);
			DateTime srv_today = ((DateTime)obj_today);

			if (sub.code != 0)
			{
				bool fAlreadyStarted = sub.from <= srv_today;
				dateSubTo.IsDisabled = sub.to < srv_today.AddDays(1);

				selSub.IsDisabled = fAlreadyStarted;
				dateSubFrom.IsDisabled = fAlreadyStarted;
				textDescription.IsDisabled = fAlreadyStarted;
			}

			selSub.ValueInt = sub.person_id;
			dateSubFrom.ValueDate = sub.from;
			dateSubTo.ValueDate = sub.to > DateTime.MinValue ? sub.to.AddDays(-1) : sub.to;
			textDescription.Value = sub.description;

			VacationClientScripts.DisplaySub(this, sub.id, dateSubTo.IsDisabled);
		}


		/// <summary>
		/// Получение руководителей организации
		/// </summary>
		/// <returns>Список руководителей организации</returns>
		private List<string> GetDirectors()
		{
			if (string.IsNullOrWhiteSpace(selCompany.Value)) return null;

			Dictionary<string, object> args = new Dictionary<string, object>();
			args.Add("@Организация", selCompany.Value);
			args.Add("@ДатаДокумента", default(DateTime) == Doc.Date ? DateTime.Now : Doc.Date);

			DataTable dtDirectors = DBManager.GetData(Kesco.Lib.Entities.SQLQueries.SELECT_Руководитель, Config.DS_person, CommandType.Text, args);

			return dtDirectors.AsEnumerable().Select(p => p.Field<int>("КодЛицаПотомка").ToString()).ToList<string>();
		}

		/// <summary>
		/// Метод запускает процесс сохранения замещений в БД
		/// </summary>
		/// <param name="fFixErrors">Если true, то требуется обязательное исправдение ошибок</param>
		void ResolveSubErrorrs(bool fFixErrors)
		{
			if (Doc.IsNew) return;

			//Добавление новых замещений
			//Можно только по одному, триггер INSTEAD OFF
			Dictionary<string, object> ins_args = new Dictionary<string, object>();
			ins_args["@КодСотрудникаЗамещаемого"] = _empl.EmployeeId;
			foreach (Substitution s in _subs.Where(sub => sub.code == 0))
			{
				ins_args["@От"] = s.from;
				ins_args["@До"] = s.to;
				ins_args["@КодСотрудникаЗамещаемого"] = _empl.EmployeeId;
				ins_args["@КодСотрудникаЗамещающего"] = s.person_id;
				ins_args["@Примечания"] = s.description;
				ins_args["@КодДокумента"] = Doc.Id/*EntityId*/;

				try
				{
					object obj_code = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.INSERT_Замещения, CommandType.Text, Config.DS_user, ins_args);
					if (obj_code != DBNull.Value)
						s.code = Decimal.ToInt32((Decimal)obj_code);
				}
				catch (DetailedException dex)
				{
					if (fFixErrors)
					{
						DisplaySubError(Resx.GetString("Vacation_ErrSubDb") + "<br/>" + dex.Message, true);
						EditSub(s);
						return;
					}
				}
			}

			if (fFixErrors)
			{
				lock (syncCacheSubs)
				{
					Cache.Remove(Doc.Id + CacheSubs);
				}

				LoadSubs();
				SendSubs();
			}

			return;
		}

		/// <summary>
		/// Метод устанавливает соответсвие между датами и продолжительностью отпуска
		/// </summary>
		/// <param name="sender">Объект, изменение которого, запустило выполнение этого метода</param>
		private void SetDaysAndDates(object sender)
		{
			//Проверяется совпадение дат заявления и дат замещения
			SendSubs();

			if (!dateFrom.ValueDate.HasValue) return;

			int nDays = numDays.ValueInt.GetValueOrDefault();

			if (!dateTo.ValueDate.HasValue && nDays < 1) return;

			DateTime d_from = dateFrom.ValueDate.GetValueOrDefault();
			DateTime d_to = dateTo.ValueDate.GetValueOrDefault().AddDays(1);

			if (dateTo.ValueDate.HasValue && (sender == dateTo || sender == null))
			{
				nDays = GetTotalDays(d_from, d_to);
			}
			else
			{
				//Подбираем дату окончания отпуска
				d_to = d_from.AddDays(nDays);

				while (nDays > GetTotalDays(d_from, d_to))
				{
					d_to = d_to.AddDays(1);
				}
			}

			//Обновляем поля формы в соответствии с расчетом
			//dateTo.ValueDate = d_to.AddDays(-1);
			//numDays.ValueInt = nDays;

			Lib.Entities.Documents.EF.Applications.Vacation vd = Doc as Lib.Entities.Documents.EF.Applications.Vacation;
			vd.DateTo.Value = d_to.AddDays(-1);
			vd.Days.Value = nDays;

			DateTime dFirstWorkingDay = GetFirstWorkingDay(d_to);
			dateFirst.Value = dFirstWorkingDay.ToString(_date_format);
		}

		/// <summary>
		/// Определение продолжительности отпуска
		/// </summary>
		/// <param name="from">Дата начала отпуска</param>
		/// <param name="to">Дата конца отпуска</param>
		/// <returns>Продолжительность отпуска</returns>
		private int GetTotalDays(DateTime from, DateTime to)
		{
			TimeSpan t = to - from;
			int nDays = (int)t.TotalDays;

			if (nDays < 1) return 0;

			return nDays - GetHolydays(from, to);
		}

		/// <summary>
		/// Определение первого рабочего дня после отпуска
		/// </summary>
		/// <param name="from">День окончания отпуска</param>
		/// <returns>первый рабочий день</returns>
		private DateTime GetFirstWorkingDay(DateTime from)
		{
			Dictionary<string, object> args = new Dictionary<string, object>();
			args.Add("@ПервыйДень", from);

			if (null != _company && _company.RegionID > 0)
			{
				args.Add("@КодТерритории", _company.RegionID);
			}
			else
			{
				args.Add("@КодТерритории", defTerritory);
			}

			object first_wd = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.SELECT_ПервыйРабочийДень, CommandType.Text, Config.DS_user, args);
			return (DateTime)first_wd;
		}

		/// <summary>
		/// Определение количества праздничных дней в указанном периоде
		/// </summary>
		/// <param name="from">Дата начала периода, включая</param>
		/// <param name="to">Дата конца периода, включая</param>
		/// <returns>Количество праздничных дней в периоде</returns>
		private int GetHolydays(DateTime from, DateTime to)
		{
			Dictionary<string, object> args = new Dictionary<string, object>();
			args.Add("@От", from);
			args.Add("@До", to);
			if (_company.RegionID > 0)
			{
				args.Add("@КодТерритории", _company.RegionID);
			}
			else
			{
				args.Add("@КодТерритории", defTerritory);
			}

			object days = DBManager.ExecuteScalar(Kesco.Lib.Entities.SQLQueries.SELECT_Праздники, CommandType.Text, Config.DS_user, args);
			return (int)days;
		}

		/// <summary>
		/// Метод проверяет, что необходимы поля о сотруднике заполнены и устанавливает фокус ввода на поле тип отпуска
		/// </summary>
		private void TestEmplInfoFilled()
		{
			if (selPerson.ValueInt.HasValue && selCompany.ValueInt.HasValue && selDirector.ValueInt.HasValue)
			{
				V4SetFocus(cbType.ID);
			}
		}

		/// <summary>
		/// Метод скрывает отображение замещений если дата отпуска в прошлом на новых документах
		/// </summary>
		private void ShowHideSubs()
		{
			if (dateTo.ValueDate.GetValueOrDefault() < DateTime.Today)
			{
				if (Doc.IsNew)
				{
					Hide("addSubRow");
				}
				else
				{
					Hide("tAddSub");
				}

				selSubForm.Visible = false;
			}
			else
			{
				Display("addSubRow");
				Display("tAddSub");

				selSubForm.Visible = true;
			}
		}

		/// <summary>
		/// Метод устанавливает фокус ввода на первое обязательное поле
		/// </summary>
		private void SetFocusToFirstEmptyField()
		{
			foreach (var ctrl in V4Controls.Values)
			{
				if (ctrl.IsRequired && !ctrl.IsReadOnly && !ctrl.IsDisabled && ctrl.Visible && ctrl.Value.Length == 0)
				{
					V4SetFocus(ctrl.ID);
					return;
				}
			}
		}
	}
}