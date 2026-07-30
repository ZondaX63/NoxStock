namespace NoxStock.Permissions;

public static class NoxStockPermissions
{
    public const string GroupName = "NoxStock";

    public static class Dashboard
    {
        public const string Default = GroupName + ".Dashboard";
    }

    public static class Products
    {
        public const string Default = GroupName + ".Products";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Export = Default + ".Export";
    }

    public static class Brands
    {
        public const string Default = GroupName + ".Brands";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class StockMovements
    {
        public const string Default = GroupName + ".StockMovements";
        public const string Create = Default + ".Create";
        public const string Adjust = Default + ".Adjust";
    }

    public static class BusinessPartners
    {
        public const string Default = GroupName + ".BusinessPartners";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Accounts
    {
        public const string Default = GroupName + ".Accounts";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Transfer = Default + ".Transfer";
    }

    public static class Documents
    {
        public const string Default = GroupName + ".Documents";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Approve = Default + ".Approve";
        public const string Cancel = Default + ".Cancel";
    }

    public static class Offers
    {
        public const string Default = GroupName + ".Offers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Convert = Default + ".Convert";
    }

    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Convert = Default + ".Convert";
    }

    public static class Sales
    {
        public const string Default = GroupName + ".Sales";
        public const string Create = Default + ".Create";
        public const string Cancel = Default + ".Cancel";
    }

    public static class Reports
    {
        public const string Default = GroupName + ".Reports";
        public const string Export = Default + ".Export";
    }

    public static class Notifications
    {
        public const string Default = GroupName + ".Notifications";
    }

    public static class AuditLogs
    {
        public const string Default = GroupName + ".AuditLogs";
    }

    public static class Settings
    {
        public const string Default = GroupName + ".Settings";
        public const string Edit = Default + ".Edit";
    }

    public static class Personnel
    {
        public const string Default = GroupName + ".Personnel";
        public const string ManagePermissions = Default + ".ManagePermissions";
    }

    public static class PrintLabels
    {
        public const string Default = GroupName + ".PrintLabels";
    }

    public static class ExchangeRates
    {
        public const string Refresh = GroupName + ".ExchangeRates.Refresh";
    }

    public static class Licenses
    {
        public const string Default = GroupName + ".Licenses";
        public const string Manage = Default + ".Manage";
        public const string ViewAudit = Default + ".ViewAudit";
    }
}
