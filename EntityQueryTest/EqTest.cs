using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using EntityQuery;
using Xunit;
using Xunit.Abstractions;

namespace EntityQuery;

[Collection("MySQLCollection")]
public partial class EqTest
{
    private readonly MySQLFixture fixture;

    private readonly ITestOutputHelper testOutputHelper;

    public EqTest(MySQLFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.testOutputHelper = testOutputHelper;

        testOutputHelper.WriteLine("EqTest construct");
    }

    DbConnection Con
    {
        get
        {
            var con = fixture.GetConnection();
            con.Open();
            return con;
        }
    }

    [Fact]
    public async Task TestEq_InsertWithSpecifiedTableName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        await con.InsertAsync(
            new User { Name = "TestEq_InsertWithSpecifiedTableName", Age = 80 },
            tx
        );

        // Then
        var user = await con.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE name='TestEq_InsertWithSpecifiedTableName' AND age=80"
        );
        Assert.NotNull(user);
        Assert.Equal(80, user!.Age);
    }

    [Fact]
    public async Task TestInsertUsingBigIntPrimaryKey()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var rowsAffected = await con.InsertAsync(new BigCar { Make = "Big", Model = "Car" }, tx);

        // Then
        Assert.Equal(1, rowsAffected);
        var bigCar = await con.QuerySingleOrDefaultAsync(
            "SELECT * FROM BigCar WHERE Make='Big' AND Model='Car'"
        );
        Assert.NotNull(bigCar);
        Assert.Equal("Big", bigCar!.Make);
        Assert.Equal("Car", bigCar!.Model);
    }

    [Fact]
    public async Task TestInsertUsingGenericLimitedFields()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var user = new User
        {
            Name = "User1",
            Age = 10,
            ScheduledDayOff = DayOfWeek.Friday // This field is not on UserEditableSettings
        };

        // When
        var id = await con.InsertAndGetIdAsync<int?, UserEditableSettings>(user, tx);

        // Then
        Assert.NotNull(id);
        var insertedUser = await con.QuerySingleOrDefaultAsync(
            $"SELECT * FROM Users WHERE Id={id.Value}",
            tx
        );
        Assert.NotNull(insertedUser);
        Assert.Null(insertedUser!.ScheduledDayOff);
    }

    [Fact]
    public async Task TestSimpleGetById()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id = await con.InsertAndGetIdAsync<int, User>(
            new User { Name = "User_TestSimpleGet", Age = 10 },
            tx
        );

        // When
        var user = await con.SelectByIdAsync<User>(id!, tx);

        // Then
        Assert.NotNull(user);
        Assert.Equal("User_TestSimpleGet", user!.Name);
    }

    [Fact]
    public async Task TestDeleteById()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id = await con.InsertAndGetIdAsync<int, User>(
            new User { Name = "TestDeleteByObject", Age = 10 },
            tx
        );

        // When
        var deleteCount = await con.DeleteByIdAsync<User>(id!, tx);

        // Then
        Assert.Equal(1, deleteCount);
        Assert.Null(await con.SelectByIdAsync<User>(id!, tx));
    }

    [Fact]
    public async Task TestDeleteByEntity()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id = await con.InsertAndGetIdAsync<int, User>(
            new User { Name = "TestDeleteByObject", Age = 10 },
            tx
        );

        // When
        var deleteCount = await con.DeleteAsync(new User { Id = id! }, tx);

        // Then
        Assert.Equal(1, deleteCount);
        Assert.Null(await con.SelectByIdAsync<User>(id!, tx));
    }

    [Fact]
    public async Task TestDeleteByWhere()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var name = "TestDeleteByObject";
        var id = await con.InsertAndGetIdAsync<int, User>(new User { Name = name, Age = 10 }, tx);

        // When
        var deleteCount = await con.DeleteAsync<User>("Name = @Name", new { Name = name }, tx);

        // Then
        Assert.Equal(1, deleteCount);
        Assert.Null(await con.SelectByIdAsync<User>(id!, tx));
    }

    [Fact]
    public async Task TestSimpleGetList()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAsync(new User { Name = "TestSimpleGetList1", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList2", Age = 11 }, tx);

        // When
        var user1 = await con.SelectAsync<User>("", new { }, tx);

        // Then
        Assert.NotNull(user1);
        Assert.Equal(2, user1!.Count());
    }

    [Fact]
    public async Task TestFilteredSelect()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAsync(new User { Name = "TestSimpleGetList1", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList2", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList3", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList4", Age = 11 }, tx);

        // When
        var user = await con.SelectAsync<User>("Age=@Age", new { Age = 10 }, tx);

        // Then
        Assert.NotNull(user);
        Assert.Equal(3, user!.Count());
    }

    [Fact]
    public async Task TestSelectAll()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAsync(new User { Name = "TestSimpleGetList1", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList2", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList3", Age = 10 }, tx);
        await con.InsertAsync(new User { Name = "TestSimpleGetList4", Age = 11 }, tx);

        // When
        var user = await con.SelectAllAsync<User>(tx);

        // Then
        Assert.NotNull(user);
        Assert.Equal(4, user!.Count());
    }

    [Fact]
    public async Task TestFilteredSelectWithMultipleKeys()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 1 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 2 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 3 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 2, Key2 = 4 }, tx);

        // When
        var keyMasters = await con.SelectAsync<KeyMaster>("Key1=@Key1", new { Key1 = 1 }, tx);
        var keyMasters2 = await con.SelectAsync<KeyMaster>("Key2=@Key2", new { Key2 = 3 }, tx);

        // Then
        Assert.NotNull(keyMasters);
        Assert.Equal(3, keyMasters.Count());
        Assert.NotNull(keyMasters2);
        Assert.Single(keyMasters2);
    }

    [Fact]
    public async Task TestFilteredSelectWithEntityMultipleKeys()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 1 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 2 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 1, Key2 = 3 }, tx);
        await con.InsertAsync(new KeyMaster { Key1 = 2, Key2 = 4 }, tx);

        // When
        var keyMasters = await con.SelectByEntityAsync(new KeyMaster { Key1 = 1, Key2 = 2 }, tx);

        // Then
        Assert.NotNull(keyMasters);
    }

    [Fact]
    public async Task TestGetWithReadonlyProperty()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        var id = await con.InsertAndGetIdAsync<int, User>(
            new User { Name = "TestGetWithReadonlyProperty", Age = 10 },
            tx
        );

        // When
        var user = await con.SelectByIdAsync<User>(id!, tx);

        // Then
        Assert.NotNull(user);
        Assert.Equal(DateTime.Now.Year, user!.CreatedDate.Year);
    }

    [Fact]
    public async Task TestInsertWithReadonlyProperty()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var id = await con.InsertAndGetIdAsync<int, User>(
            new User
            {
                Name = "TestInsertWithReadonlyProperty",
                Age = 10,
                CreatedDate = new DateTime(2001, 1, 1)
            },
            tx
        );

        // Then
        var user = await con.SelectByIdAsync<User>(id!, tx);
        Assert.NotNull(user);
        Assert.Equal(DateTime.Now.Year, user!.CreatedDate.Year);
    }

    [Fact]
    public async Task TestUpdateWithNotMappedProperty()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id = await con.InsertAndGetIdAsync<int, User>(
            new User { Name = "TestUpdateWithNotMappedProperty", Age = 10 },
            tx
        );

        // When
        var user = await con.SelectByIdAsync<User>(id!, tx);
        user!.Age = 11;
        user!.CreatedDate = new DateTime(2001, 1, 1);
        user!.NotMappedInt = 1234;
        await con.UpdateAsync(user, tx);

        // Then
        user = await con.SelectByIdAsync<User>(id!, tx);
        Assert.Equal(0, user!.NotMappedInt);
    }

    [Fact]
    public async Task TestInsertWithSpecifiedKey()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var id1 = await con.InsertAndGetIdAsync<int, Car>(
            new Car { Make = "Honda", Model = "Civic" },
            tx
        );
        var id2 = await con.InsertAndGetIdAsync<int, Car>(
            new Car { Make = "Honda", Model = "Civic" },
            tx
        );

        // Then
        Assert.Equal(1, id2 - id1);
    }

    [Fact]
    public async Task TestInsertWithExtraPropertiesShouldSkipNonSimpleTypesAndPropertiesMarkedEditableFalse()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var id = await con.InsertAndGetIdAsync<int, Car>(
            new Car
            {
                Make = "Honda",
                Model = "Civic",
                Users = new List<User>
                {
                    new User { Age = 12, Name = "test" }
                }
            },
            tx
        );

        // Then
        var car = await con.SelectByIdAsync<Car>(id!, tx);
        Assert.NotNull(car);
        Assert.Empty(car!.Users);
    }

    [Fact]
    public async Task TestUpdate()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var newid = await con.InsertAndGetIntIdAsync(
            new Car { Make = "Honda", Model = "Civic" },
            tx
        );

        // When
        var newitem = await con.SelectByIdAsync<Car>(newid!, tx);
        newitem!.Make = "Toyota";
        await con.UpdateAsync(newitem, tx);

        // Then
        var updateditem = await con.SelectByIdAsync<Car>(newid!, tx);
        Assert.NotNull(updateditem);
        Assert.Equal("Toyota", updateditem!.Make);
    }

    /// <summary>
    /// We expect scheduled day off to NOT be updated, since it's not a property of UserEditableSettings
    /// </summary>
    [Fact]
    public async Task TestUpdateUsingGenericLimitedFields()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        var user = new User
        {
            Name = "User1",
            Age = 10,
            ScheduledDayOff = DayOfWeek.Friday
        };
        user.Id = await con.InsertAndGetIntIdAsync(user, tx);
        user.ScheduledDayOff = DayOfWeek.Thursday;

        // When
        var userAsEditableSettings = (UserEditableSettings)user;
        userAsEditableSettings.Name = "User++";
        await con.UpdateAsync(userAsEditableSettings, tx);

        // Then
        var insertedUser = await con.SelectByIdAsync<User>(user.Id, tx);
        Assert.NotNull(insertedUser);
        Assert.Equal("User++", insertedUser!.Name);
        Assert.Equal(DayOfWeek.Friday, insertedUser.ScheduledDayOff);
    }

    [Fact]
    public async Task TestDeleteByObjectWithAttributes()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id = await con.InsertAndGetIntIdAsync(new Car { Make = "Honda", Model = "Civic" }, tx);
        var car = await con.SelectByIdAsync<Car>(id!, tx);

        // When
        await con.DeleteAsync(car!, tx);

        // Then
        Assert.Null(await con.SelectByIdAsync<Car>(id!, tx));
    }

    [Fact]
    public async Task TestDeleteByMultipleKeyObjectWithAttributes()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var keyMaster = new KeyMaster { Key1 = 1, Key2 = 2 };
        await con.InsertAsync(keyMaster, tx);
        var car = await con.SelectByIdAsync<KeyMaster>(new { Key1 = 1, Key2 = 2 }, tx);

        // When
        await con.DeleteAsync(car!, tx);

        // Then
        Assert.Null(await con.SelectByEntityAsync<KeyMaster>(keyMaster, tx));
    }

    [Fact]
    public async Task TestNullableSimpleTypesAreSaved()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var id = await con.InsertAndGetIntIdAsync(
            new User1
            {
                Name = "User",
                Age = 11,
                ScheduledDayOff = 2
            },
            tx
        )!;

        // Then
        var user1 = await con.SelectByIdAsync<User1>(id, tx);
        Assert.NotNull(user1);
        Assert.Equal(2, user1!.ScheduledDayOff);
    }

    [Fact]
    public async Task TestGetFromTableWithNonIntPrimaryKey()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAsync<City>(new City { Name = "Morgantown", Population = 31000 });

        // When
        var city = await con.SelectByIdAsync<City>("Morgantown", tx);

        // Then
        Assert.NotNull(city);
        Assert.Equal(31000, city!.Population);
    }

    [Fact]
    public async Task TestDeleteFromTableWithNonIntPrimaryKey()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAsync<City>(new City { Name = "Fairmont", Population = 18737 });

        // When
        var deletedCount = await con.DeleteByIdAsync<City>("Fairmont", tx);

        // Then
        Assert.Equal(1, deletedCount);
    }

    [Fact]
    public async Task TestNullableEnumInsert()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        await con.InsertAsync(
            new User
            {
                Name = "Enum-y",
                Age = 10,
                ScheduledDayOff = DayOfWeek.Thursday
            },
            tx
        );

        // Then
        var userList = await con.SelectAsync<User>("name=@Name", new { Name = "Enum-y" }, tx);
        var user = userList.FirstOrDefault();
        Assert.NotNull(user);
        Assert.Equal(DayOfWeek.Thursday, user!.ScheduledDayOff);
    }

    [Fact]
    public async Task TestInsertIntoTableWithStringKey()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        await con.InsertAsync(new StringTest { stringkey = "123xyz", name = "Bob" }, tx);

        // Then
        var entity = await con.QuerySingleOrDefaultAsync<StringTest>(
            "SELECT * FROM StringTest WHERE stringkey='123xyz'",
            transaction: tx
        );
        Assert.NotNull(entity);
        Assert.Equal("123xyz", entity.stringkey);
        Assert.Equal("Bob", entity.name);
    }

    [Fact]
    public async Task TestMultiInsertAsync()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        await Task.WhenAll(
            con.InsertAsync(new User { Name = "TestMultiInsertASync1", Age = 10 }, tx),
            con.InsertAsync(new User { Name = "TestMultiInsertASync2", Age = 10 }, tx),
            con.InsertAsync(new User { Name = "TestMultiInsertASync3", Age = 10 }, tx),
            con.InsertAsync(new User { Name = "TestMultiInsertASync4", Age = 11 }, tx)
        );

        // Then
        var list = await con.SelectAsync<User>("age=@Age", new { Age = 10 }, tx);
        Assert.Equal(3, list.Count());
    }

    [Fact]
    public async Task TestDeleteByMultipleKeyObject()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var keyMaster = new KeyMaster { Key1 = 1, Key2 = 2 };
        await con.InsertAsync(keyMaster, tx);

        // When
        int affectedRows = await con.DeleteByIdAsync<KeyMaster>(new { Key1 = 1, Key2 = 2 }, tx);

        // Then
        Assert.Equal(1, affectedRows);
        Assert.Null(await con.SelectByEntityAsync(keyMaster, tx));
    }

    [Fact]
    public async Task TestInsertWithSpecifiedPrimaryKeyAsync()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var rowsAffected = await con.InsertAsync(
            new UserWithoutAutoIdentity()
            {
                Id = 999,
                Name = "User999Async",
                Age = 10
            },
            tx
        );

        // Then
        var user = await con.SelectByIdAsync<UserWithoutAutoIdentity>(999, tx);
        Assert.Equal(1, rowsAffected);
        Assert.Equal("User999Async", user!.Name);
        Assert.Equal(10, user!.Age);
    }

    [Fact]
    public async Task TestInsertWithSpecifiedColumnName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var itemId = await con.InsertAndGetIdAsync<int, StrangeColumnNames>(
            new StrangeColumnNames
            {
                Word = "InsertWithSpecifiedColumnName",
                StrangeWord = "Strange 1"
            },
            tx
        );

        // Then
        Assert.True(itemId > 0);
    }

    [Fact]
    public async Task TestDeleteByObjectWithSpecifiedColumnName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var itemId = await con.InsertAndGetIdAsync<int, StrangeColumnNames>(
            new StrangeColumnNames
            {
                Word = "TestDeleteByObjectWithSpecifiedColumnName",
                StrangeWord = "Strange 1"
            },
            tx
        );

        // When
        var strange = await con.SelectByIdAsync<StrangeColumnNames>(itemId!, tx);
        var affectedRows = await con.DeleteAsync(strange!, tx);

        // Then
        Assert.Equal(1, affectedRows);
        Assert.Null(await con.SelectByIdAsync<StrangeColumnNames>(itemId!, tx));
    }

    [Fact]
    public async Task TestSimpleGetListWithSpecifiedColumnName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var id1 = con.InsertAndGetIntIdAsync(
            new StrangeColumnNames
            {
                Word = "TestSimpleGetListWithSpecifiedColumnName1",
                StrangeWord = "Strange 2",
            },
            tx
        );
        var id2 = con.InsertAndGetIntIdAsync(
            new StrangeColumnNames
            {
                Word = "TestSimpleGetListWithSpecifiedColumnName2",
                StrangeWord = "Strange 3",
            },
            tx
        );

        // When
        var strange = await con.SelectAllAsync<StrangeColumnNames>(tx);

        // Then
        Assert.Equal(2, strange.Count());
    }

    [Fact]
    public async Task TestUpdateWithSpecifiedColumnName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        var newid = await con.InsertAndGetIntIdAsync(
            new StrangeColumnNames { Word = "Word Insert", StrangeWord = "Strange Insert" },
            tx
        )!;

        // When
        var newitem = await con.SelectByIdAsync<StrangeColumnNames>(newid, tx);
        newitem!.Word = "Word Update";
        await con.UpdateAsync(newitem, tx);

        // Then
        var updatedItem = await con.SelectByIdAsync<StrangeColumnNames>(newid, tx);
        Assert.Equal("Word Update", updatedItem!.Word);
    }

    [Fact]
    public async Task TestFilteredGetListWithSpecifiedColumnName()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        await con.InsertAndGetIntIdAsync(
            new StrangeColumnNames { Word = "Word 5", StrangeWord = "Strange 1", },
            tx
        );
        await con.InsertAndGetIntIdAsync(
            new StrangeColumnNames { Word = "Word 6", StrangeWord = "Strange 2", },
            tx
        );
        await con.InsertAndGetIntIdAsync(
            new StrangeColumnNames { Word = "Word 7", StrangeWord = "Strange 2", },
            tx
        );
        await con.InsertAndGetIntIdAsync(
            new StrangeColumnNames { Word = "Word 8", StrangeWord = "Strange 2", },
            tx
        );

        // When
        var results = await con.SelectAsync<StrangeColumnNames>(
            "colstringstrangeword=@StrangeWord",
            new { StrangeWord = "Strange 2" },
            tx
        );

        // Then
        Assert.Equal(3, results.Count());
    }

    [Fact]
    public async Task TestDeleteListWithWhereClause()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        int x = 0;
        do
        {
            await con.InsertAsync(
                new User
                {
                    Name = "Person " + x,
                    Age = x,
                    CreatedDate = DateTime.Now,
                    ScheduledDayOff = DayOfWeek.Thursday
                },
                tx
            );
            x++;
        } while (x < 30);

        // When
        await con.DeleteAsync<User>("age > 9", new { }, tx);

        // Then
        var resultlist = await con.SelectAllAsync<User>(tx);
        Assert.Equal(10, resultlist.Count());
    }

    [Fact]
    public async Task TestDeleteListWithWhereObject()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();
        int x = 0;
        do
        {
            await con.InsertAsync(
                new User
                {
                    Name = "Person " + x,
                    Age = x,
                    CreatedDate = DateTime.Now,
                    ScheduledDayOff = DayOfWeek.Thursday
                },
                tx
            );
            x++;
        } while (x < 10);

        // When
        await con.DeleteAsync<User>("age=@Age", new { Age = 9 }, tx);

        // Then
        var resultlist = await con.SelectAllAsync<User>(tx);
        Assert.Equal(9, resultlist.Count());
    }

    [Fact]
    public async Task TestIgnorePropertiesForInsertSelect()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var itemId = await con.InsertAndGetIntIdAsync(
            new IgnoreColumns()
            {
                IgnoreInsert = "OriginalInsert",
                IgnoreUpdate = "OriginalUpdate",
                IgnoreSelect = "OriginalSelect",
                IgnoreAll = "OriginalAll"
            },
            tx
        );
        var item = await con.SelectByIdAsync<IgnoreColumns>(itemId!, tx);

        // Then
        //verify insert column was ignored
        Assert.Null(item!.IgnoreInsert);
        //verify select value wasn't selected
        Assert.Null(item!.IgnoreSelect);
        //verify the column is really there via straight dapper
        var fromDapper = con.Query<IgnoreColumns>(
                "Select * from IgnoreColumns where Id = @Id",
                new { id = itemId },
                tx
            )
            .First();
        Assert.Equal("OriginalSelect", fromDapper.IgnoreSelect);
    }

    [Fact]
    public async Task TestIgnorePropertiesForUpdate()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        var itemId = await con.InsertAndGetIntIdAsync(
            new IgnoreColumns()
            {
                IgnoreInsert = "OriginalInsert",
                IgnoreUpdate = "OriginalUpdate",
                IgnoreSelect = "OriginalSelect",
                IgnoreAll = "OriginalAll"
            },
            tx
        );
        var item = await con.SelectByIdAsync<IgnoreColumns>(itemId!, tx);

        // When
        item!.IgnoreUpdate = "ChangedUpdate";
        await con.UpdateAsync(item, tx);

        // Then
        item = await con.SelectByIdAsync<IgnoreColumns>(itemId!, tx);
        Assert.Equal("OriginalUpdate", item!.IgnoreUpdate);
        //verify the column is really there via straight dapper
        var allColumnDapper = con.Query<IgnoreColumns>(
                "Select IgnoreAll from IgnoreColumns where Id = @Id",
                new { id = itemId },
                tx
            )
            .First();
        Assert.Null(allColumnDapper.IgnoreAll);
    }

    [Fact]
    public async Task TestUpsertOnUpdate()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        var id = await con.InsertAndGetIntIdAsync(new User { Name = "user1", Age = 30, }, tx);

        // When
        var upsertedCount = await con.UpsertAsync(
            new User
            {
                Id = id,
                Name = "user1_updated",
                Age = 31
            },
            tx
        );

        // Then
        Assert.Equal(2, upsertedCount); // update는 rows affected 가 2이다.
        var user = await con.SelectByIdAsync<User>(id!, tx);
        Assert.Equal("user1_updated", user!.Name);
        Assert.Equal(31, user!.Age);
    }

    [Fact]
    public async Task TestUpsertAfterDelete()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        var id = await con.InsertAndGetIntIdAsync(new User { Name = "user1", Age = 30, }, tx);
        var user = await con.SelectByIdAsync<User>(id!, tx);
        await con.DeleteAsync<User>(user!, tx);

        // When
        user!.Name = "user1_inserted";
        var upsertedCount = await con.UpsertAsync(user!, tx);

        // Then
        Assert.Equal(1, upsertedCount);
        var user2 = await con.SelectByIdAsync<User>(id!, tx);
        Assert.Equal("user1_inserted", user2!.Name);
        Assert.Equal(30, user2!.Age);
    }

    [Fact]
    public async Task TestInsertList()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var userList = new List<User>
        {
            new User { Name = "user1", Age = 31, },
            new User { Name = "user2", Age = 32, },
        };
        var rowCount = await con.InsertListAsync<User>(userList, tx);

        // Then
        Assert.Equal(2, rowCount);
        Assert.Equal(2, (await con.SelectAllAsync<User>()).Count());
    }

    [Fact]
    public async Task TestUpsertList()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var userList = new List<User>
        {
            new User { Name = "user1", Age = 31, },
            new User { Name = "user2", Age = 32, },
        };
        var rowCount = await con.UpsertListAsync<User>(userList, tx);

        // Then
        Assert.Equal(2, rowCount);
        Assert.Equal(2, (await con.SelectAllAsync<User>()).Count());
    }

    [Fact]
    public async Task TestUserAsRecordSimpleGet()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        var id = await con.InsertAndGetIntIdAsync(
            new UserRecord(0, "UserRecordTestSimpleGet", 10, null),
            tx
        );

        // Then
        var user = await con.SelectByIdAsync<User>(id!, tx);
        Assert.NotNull(user);
        Assert.Equal("UserRecordTestSimpleGet", user!.Name);
    }

    [Fact]
    public async Task TestRecordGetEntityWithKeyAttribute()
    {
        // Given
        using var con = Con;
        using var tx = con.BeginTransaction();

        // When
        await con.InsertAsync(new CityRecord("CityName", 1000), tx);

        // Then
        var city = await con.SelectByIdAsync<CityRecord>("CityName", tx);
        Assert.NotNull(city);
        Assert.Equal("CityName", city!.Name);
    }
}
