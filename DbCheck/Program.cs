using System;
using System.Data;
using Dapper;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=localhost;Port=5432;Database=qualiflowdb;Username=postgres;Password=root;SSL Mode=Disable;";
        Console.WriteLine("Connecting to: " + connStr);
        try
        {
            using var connection = new NpgsqlConnection(connStr);
            connection.Open();
            Console.WriteLine("Connection successful!");

            var usersCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM \"Users\"");
            Console.WriteLine($"Total users: {usersCount}");

            var superAdmins = connection.Query<dynamic>("SELECT \"Id\", \"Email\", \"Role\", \"IsActive\", \"OrganizationId\" FROM \"Users\" WHERE \"Role\" = 'SUPER_ADMIN'");
            Console.WriteLine($"Super admins found: {superAdmins.AsList().Count}");
            foreach (var sa in superAdmins)
            {
                Console.WriteLine($"- Id: {sa.Id}, Email: {sa.Email}, Active: {sa.IsActive}, OrgId: {sa.OrganizationId}");
            }

            var notificationsCount = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM \"Notifications\"");
            Console.WriteLine($"Total notifications: {notificationsCount}");

            var orgReqNotifications = connection.Query<dynamic>("SELECT * FROM \"Notifications\" WHERE \"ReferenceType\" = 'ORGANIZATION_REQUEST'");
            Console.WriteLine($"Organization requests in Notifications: {orgReqNotifications.AsList().Count}");
            foreach (var n in orgReqNotifications)
            {
                Console.WriteLine($"- Id: {n.Id}, UserId: {n.UserId}, Title: {n.Title}, Message: {n.Message}, CreatedAt: {n.CreatedAt}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
