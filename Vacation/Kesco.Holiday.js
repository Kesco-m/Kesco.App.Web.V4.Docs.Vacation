"use strict";
/*
Эти ресурсы передаются в клиентское приложение от Вэб сервера
var StrResources = {
"BtnSave": "Сохранить",
"BtnCancel": "Отмена",
"BtnDelete": "Удалить",
"BtnYes": "Да",
"BtnNo": "Нет",
"From": "c&nbsp;",
"To": "по&nbsp;",
"SubTitle": "Замещение сотрудника",
"SubDelete": "Удалить замещение?",
"SubDeleteTitle": "Удалить замещение",
"SubDeleteAlt": "Удалить"
};
*/

/*
* Формирует HTML таблицу из полученых XML данных
* @xml_data - данные в формате XML
*/
function displaySubTable(xml_data) {

    var xmlDoc;

    if (window.DOMParser) {
        var parser = new DOMParser();
        xmlDoc = parser.parseFromString(xml_data, "text/xml");
    }
    else // Internet Explorer
    {
        xmlDoc = new ActiveXObject("Microsoft.XMLDOM");
        xmlDoc.async = false;
        xmlDoc.loadXML(xml_data);
    }

    var lineItems = xmlDoc.getElementsByTagName("sub");
    var html = "<tbody>";
    for (var i = 0; i < lineItems.length; i++) {
        var disabled = lineItems[i].getAttribute("disabled");
        var description = lineItems[i].getAttribute("description");

        html += "<tr><td width='16'>"
        + (disabled != 1 ? "<input alt='" + StrResources.SubDeleteAlt + "' title='" + StrResources.SubDeleteTitle + "' onclick='deleteSub(" + lineItems[i].getAttribute("id") + ",false)' type='image' src='/Styles/Delete.gif' />" : "")
        + "</td><td><a href='javascript: editSub("
        + lineItems[i].getAttribute("id") + ");'>"
        + lineItems[i].getAttribute("person");

        if (description)
            html += "&nbsp;(" + description + ")&nbsp;"

        html += "</a></td></tr>"
    }

    html += "</tbody>";

    document.getElementById("tSub").innerHTML = html;
}

/*
* Добавить новое замещение
*/
function addSub() {
    cmd('cmd', 'save_sub', 'sub_id', -1);
}

/*
* Удалить замещение
* @sub_id - идентификатор замещения в приложении
* @close_dlg - признак того, что функция вызвана из диалога редактирования замещения, котрый наобходимо закрыть
*/
function deleteSub(sub_id, close_dlg) {
    v4_showConfirm(StrResources.SubDelete, StrResources.SubTitle, StrResources.BtnYes, StrResources.BtnNo, "cmd('cmd', 'del_sub', 'sub_id', " + sub_id + ", 'close_dlg', " + close_dlg + ");", null);
}

/*
* Редактировать замещение
* @sub_id - идентификатор замещения в приложении
*/
function editSub(sub_id) {
    cmd('cmd', 'edit_sub', 'sub_id', sub_id);
}

/*
* Сохранить замещение
* @sub_id - идентификатор замещения в приложении
*/
function saveSub(sub_id) {
    cmd('cmd', 'save_sub', 'sub_id', sub_id);
}

/*
* Функция обработки события зарытия диалога редактирования замещения
*/
function onCloseSub() {
    //Необходимо дать польностью выполниться вызывающему методу, и только потом иницировать вызов close_sub
    setTimeout(function () { cmd('cmd', 'close_sub'); }, 0);
}

//Диалог редактирования замещения
displaySub.sub_dialog = null;

/*
* Функция создает и показывает диалог редактирования замещения
* @sub_id - идентификатор замещения в приложении
* @read_only - признак того, что изменение замещения запрещено
*/
function displaySub(sub_id, read_only) {

    var dlgSelector = "#editSub";
   if (null == displaySub.sub_dialog) {

       displaySub.sub_dialog = $(dlgSelector).dialog({
           autoOpen: false,
           resizable: false,
           modal: true,
           title: StrResources.SubTitle,
           width: 380,
           //open: function () { $("#btnSave").focus();},
           close: function (event, ui) { onCloseSub(); }
       });
    }

   if (read_only) $(dlgSelector).on("dialogopen", function (event, ui) { $("#btnDlgCancel").focus(); });
   else $(dlgSelector).on("dialogopen", function (event, ui) { $("#btnDlgSave").focus(); });

    var buttons = read_only ? [] : [
        { id: 'btnDlgSave', text: StrResources.BtnSave, click: function () { saveSub(sub_id); } },
        { id: 'btnDlgDelete', text: StrResources.BtnDelete, click: function () { deleteSub(sub_id, true); } }
    ];

    var cancel_btn = { id: 'btnDlgCancel', text: StrResources.BtnCancel, click: function () { $(this).dialog('close'); } };
    buttons.push(cancel_btn);

    displaySub.sub_dialog.dialog("option", "buttons", buttons);

    displaySub.sub_dialog.dialog('open');
}

/*
* Функция закрывает диалог редактирования замещения, если он открыт
*/
function closeSubDialog() {
    if (displaySub.sub_dialog) {
        displaySub.sub_dialog.dialog('close');
    }
}