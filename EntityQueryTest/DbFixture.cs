using System;
using System.Data;
using System.Data.Common;
using Dapper;
using MySql.Data.MySqlClient;
using Xunit;

namespace EntityQuery;

public class MySqlGuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid guid)
    {
        parameter.Value = guid.ToString();
    }

    public override Guid Parse(object value)
    {
        return new Guid((string)value);
    }
}

public class MySQLFixture : IDisposable
{
    public DbConnection GetConnection(string dbName = "testdb")
    {
        return new MySqlConnection(
            String.Format(
                "Server={0};Port={1};User Id={2};Password={3};Database={4};",
                "127.0.0.1", //"localhost",
                "13306", //"3306",
                "root",
                "1234qwer", // "admin",
                dbName
            // "localhost",
            // "3306",
            // "root",
            // "admin",
            // "sys"
            )
        );
    }

    public MySQLFixture()
    {
        using (var con = GetConnection("sys"))
        {
            // drop database
            con.Execute("DROP DATABASE IF EXISTS testdb;");
            con.Execute("CREATE DATABASE testdb;");
        }

        System.Threading.Thread.Sleep(1000);

        using (var con = GetConnection())
        {
            con.Open();

            con.Execute(
                @" create table Users (Id INTEGER PRIMARY KEY AUTO_INCREMENT, Name nvarchar(100) not null, Age int not null, ScheduledDayOff int null, CreatedDate datetime default current_timestamp ) "
            );
            con.Execute(
                @" create table Car (CarId INTEGER PRIMARY KEY AUTO_INCREMENT, Id INTEGER null, Make nvarchar(100) not null, Model nvarchar(100) not null) "
            );
            con.Execute(
                @" create table BigCar (CarId BIGINT PRIMARY KEY AUTO_INCREMENT, Make nvarchar(100) not null, Model nvarchar(100) not null) "
            );
            con.Execute(@" insert into BigCar (CarId,Make,Model) Values (2147483649,'car','car') ");
            con.Execute(
                @" create table City (Name nvarchar(100) not null, Population int not null) "
            );
            con.Execute(
                @" create table StrangeColumnNames (ItemId INTEGER PRIMARY KEY AUTO_INCREMENT, word nvarchar(100) not null, colstringstrangeword nvarchar(100) not null, KeywordedProperty nvarchar(100) null) "
            );
            con.Execute(
                @" create table UserWithoutAutoIdentity (Id INTEGER PRIMARY KEY, Name nvarchar(100) not null, Age int not null) "
            );
            con.Execute(
                @" create table IgnoreColumns (Id INTEGER PRIMARY KEY AUTO_INCREMENT, IgnoreInsert nvarchar(100) null, IgnoreUpdate nvarchar(100) null, IgnoreSelect nvarchar(100)  null, IgnoreAll nvarchar(100) null) "
            );
            con.Execute(
                @" CREATE table KeyMaster (Key1 INTEGER NOT NULL, Key2 INTEGER NOT NULL, CONSTRAINT PK_KeyMaster PRIMARY KEY CLUSTERED (Key1 ASC, Key2 ASC))"
            );
            con.Execute(
                @" CREATE TABLE StringTest (stringkey varchar(50) NOT NULL, name varchar(50) NOT NULL, CONSTRAINT PK_stringkey PRIMARY KEY CLUSTERED (stringkey ASC))"
            );
        }

        // For tests regarding GUID
        SqlMapper.AddTypeHandler(new MySqlGuidTypeHandler());
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));
    }

    public void Dispose()
    {
        // using (var con = GetConnection())
        // {
        //     // drop  database
        //     con.Execute("DROP DATABASE IF EXISTS testdb;");
        // }
    }
}

[CollectionDefinition("MySQLCollection")]
public class DatabaseCollection : ICollectionFixture<MySQLFixture> { }
