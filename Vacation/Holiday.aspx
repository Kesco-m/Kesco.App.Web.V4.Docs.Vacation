<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Holiday.aspx.cs" Inherits="Kesco.App.Web.Docs.Vacation.Holiday" ShowCopyButton="False"%>
<%@ Register TagPrefix="v4dbselect" Namespace="Kesco.Lib.Web.DBSelect.V4" Assembly="DBSelect.V4" %>
<%@ Register TagPrefix="v4control" Namespace="Kesco.Lib.Web.Controls.V4" Assembly="Controls.V4" %>
<%@ Import Namespace="Kesco.Lib.Localization" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <script src='Kesco.Holiday.js' type='text/javascript'></script>
    <style type="text/css">
        #editSub
        {
            display: none;
        }
        .spacer
        {
            display:block;
            height: 5px;
        }
        #addSubRow
        {
            width: 540px;
            margin-top: 5px;
            margin-bottom: 5px;
            padding: 5px;
        }
        #SubErrMsg
        {
            color: Red;
        }
        
    </style>
</head>
<body>
<%=RenderDocumentHeader()%>
<div class="spacer"></div>
<div class="v4formContainer">
<%RenderDocNumDateNameRows(Response.Output);%>
<div class="spacer"></div>
<table>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_Person")%>:</td>
        <td><v4dbselect:DBSEmployee ID="selPerson" runat="server" IsCaller="True" CLID="4"  AutoSetSingleValue="True" CallerType="Person" IsRequired="True" Width="360px" NextControl="selCompany" OnBeforeSearch="selPerson_BeforeSearch"  OnChanged="selPerson_Changed"></v4dbselect:DBSEmployee></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_Company")%>:</td>
        <td><v4dbselect:DBSPerson ID="selCompany" runat="server" IsCaller="True" CLID="4" AutoSetSingleValue="True" CallerType="Person" IsRequired="True" Width="360px" NextControl="selDirector" OnBeforeSearch="selCompany_BeforeSearch" OnChanged="selCompany_Changed"></v4dbselect:DBSPerson></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_Director")%>:</td>
        <td><v4dbselect:DBSPerson ID="selDirector" runat="server" IsCaller="True" CLID="4" AutoSetSingleValue="True" CallerType="Person" IsRequired="True" Width="360px" NextControl="cbType" OnBeforeSearch="selDirector_BeforeSearch"  OnChanged="selDirector_Changed"></v4dbselect:DBSPerson></td>
    </tr>
    <tr>
        <td colspan="2" align="center"><i><%= Resources.Resx.GetString("Vacation_DocTitle")%></i></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_Type")%>:</td>
        <td><v4dbselect:DBSVacationType ID="cbType" runat="server" IsRequired="True" Width="360px" AutoSetSingleValue="True" NextControl="dateFrom" OnChanged="cbType_Changed" OnBeforeSearch="cbType_BeforeSearch"></v4dbselect:DBSVacationType></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_From")%>:</td>
        <td><v4control:DatePicker ID="dateFrom" runat="server" IsRequired="True" NextControl="numDays" OnChanged="DaysOrDates_Changed"></v4control:DatePicker></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_Days")%>:</td>
        <td><v4control:Number ID="numDays" runat="server" IsRequired="True" Width="30px" NextControl="dateTo" OnChanged="DaysOrDates_Changed"></v4control:Number></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_To")%>:</td>
        <td><v4control:DatePicker ID="dateTo" runat="server" IsRequired="True" NextControl="selSubForm" OnChanged="DaysOrDates_Changed"></v4control:DatePicker></td>
    </tr>
    <tr id="firstDay">
        <td><%= Resources.Resx.GetString("Vacation_FirstWorkingDay")%>:</td>
        <td><v4control:Div ID="dateFirst" runat="server"></v4control:Div></td>
    </tr>
</table>

<fieldset id="addSubRow">
<legend><%= Resources.Resx.GetString("Vacation_SubTitle")%></legend>
<table id="tSub"></table>
<table id="tAddSub">
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_SubEmployee")%>:</td>
        <td><v4dbselect:DBSEmployee ID="selSubForm" runat="server" IsCaller="True" AutoSetSingleValue="True" CLID="4" CallerType="Person" Width="250px" OnBeforeSearch="selSub_BeforeSearch"></v4dbselect:DBSEmployee></td>
        <td><input id="btnAddSubForm" runat="server" onclick='addSub();' type='image' src='/Styles/New.gif' /></td>
    </tr>
</table>
</fieldset>

<% StartRenderVariablePart(Response.Output); %>
<% EndRenderVariablePart(Response.Output); %>
</div>

<div id="editSub">
<table>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_SubEmployee")%>:</td>
        <td><v4dbselect:DBSEmployee ID="selSub" runat="server" IsCaller="True" CLID="4" AutoSetSingleValue="True" CallerType="Person" Width="250px" IsRequired="True" OnBeforeSearch="selSub_BeforeSearch"></v4dbselect:DBSEmployee></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_SubFrom")%>:</td>
        <td><v4control:DatePicker ID="dateSubFrom" runat="server" IsRequired="True"></v4control:DatePicker></td>
    </tr>
    <tr>
        <td><%= Resources.Resx.GetString("Vacation_SubTo")%>:</td>
        <td><v4control:DatePicker ID="dateSubTo" runat="server" IsRequired="True"></v4control:DatePicker></td>
    </tr>
     <tr>
        <td><%= Resources.Resx.GetString("Vacation_SubDescription")%>:</td>
        <td><v4control:TextArea ID="textDescription" runat="server" Width="270px" Height="60px"></v4control:TextArea></td>
    </tr>
</table>
<v4control:Div ID="SubErrMsg" runat="server"></v4control:Div>
</div>

</body>
</html>
