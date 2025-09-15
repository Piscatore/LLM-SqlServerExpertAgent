using FluentAssertions;
using SqlServerExpertAgent.Plugins;
using System.Reflection;

namespace SqlServerExpertAgent.Tests.Plugins;

/// <summary>
/// Unit tests for GitSchemaPlugin that can run without Git or SQL Server dependencies
/// Uses reflection to test internal logic and data structures
/// </summary>
public class GitSchemaPluginUnitTests
{
    #region Plugin Metadata Tests

    [Fact]
    public void Metadata_HasCorrectDependencies()
    {
        // Arrange
        var plugin = new GitSchemaPlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.Dependencies.Should().Contain("SqlServerPlugin");
        metadata.Dependencies.Should().HaveCount(1);
    }

    [Fact]
    public void Metadata_HasCorrectCapabilities()
    {
        // Arrange
        var plugin = new GitSchemaPlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.SqlSchema);
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.FileOperations);
        metadata.Capabilities.Should().NotHaveFlag(PluginCapabilities.SqlQuery);
        metadata.Capabilities.Should().NotHaveFlag(PluginCapabilities.SqlOptimization);
    }

    [Fact]
    public void Metadata_HasGitRequirements()
    {
        // Arrange
        var plugin = new GitSchemaPlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.CustomProperties["GitRequired"].Should().Be(true);
        metadata.CustomProperties["SupportedGitVersion"].Should().Be("2.0+");
        
        var formats = metadata.CustomProperties["SchemaFormats"] as string[];
        formats.Should().Contain(new[] { "SQL", "JSON", "DACPAC" });
    }

    #endregion

    #region Schema Comparison Logic Tests

    [Fact]
    public void SchemaDifference_Record_CreatesCorrectly()
    {
        // Arrange
        var changeType = "Added";
        var objectType = "Table";
        var objectName = "dbo.NewTable";
        var details = "New table added";

        // Act
        var difference = new SchemaDifference(changeType, objectType, objectName, details);

        // Assert
        difference.ChangeType.Should().Be(changeType);
        difference.ObjectType.Should().Be(objectType);
        difference.ObjectName.Should().Be(objectName);
        difference.Details.Should().Be(details);
    }

    [Fact]
    public void DatabaseSchema_WithCompleteData_StoresCorrectly()
    {
        // Arrange
        var dbName = "TestDatabase";
        var extractedAt = DateTime.UtcNow;
        var tables = new List<TableSchema>
        {
            new("dbo", "Users", 
                new List<ColumnSchema> { new("Id", "int", false, true, true, null) },
                new List<IndexSchema>(),
                new List<ConstraintSchema>())
        };
        var views = new List<ViewSchema>
        {
            new("dbo", "ActiveUsers", "SELECT * FROM Users WHERE Active = 1")
        };
        var procedures = new List<StoredProcedureSchema>
        {
            new("dbo", "GetUser", "SELECT * FROM Users WHERE Id = @Id", 
                new List<ParameterSchema> { new("@Id", "int", false, null) })
        };
        var functions = new List<FunctionSchema>
        {
            new("dbo", "GetUserCount", "RETURN (SELECT COUNT(*) FROM Users)", "int",
                new List<ParameterSchema>())
        };

        // Act
        var schema = new DatabaseSchema(dbName, extractedAt, tables, views, procedures, functions);

        // Assert
        schema.DatabaseName.Should().Be(dbName);
        schema.ExtractedAt.Should().Be(extractedAt);
        schema.Tables.Should().HaveCount(1);
        schema.Views.Should().HaveCount(1);
        schema.StoredProcedures.Should().HaveCount(1);
        schema.Functions.Should().HaveCount(1);
        
        schema.Tables[0].Name.Should().Be("Users");
        schema.Views[0].Name.Should().Be("ActiveUsers");
        schema.StoredProcedures[0].Name.Should().Be("GetUser");
        schema.Functions[0].Name.Should().Be("GetUserCount");
    }

    #endregion

    #region Schema Object Model Tests

    [Fact]
    public void TableSchema_WithIndexesAndConstraints_StoresCompleteInformation()
    {
        // Arrange
        var columns = new List<ColumnSchema>
        {
            new("Id", "int", false, true, true, null),
            new("Email", "nvarchar(255)", false, false, false, null),
            new("CreatedDate", "datetime2", false, false, false, "GETUTCDATE()")
        };
        
        var indexes = new List<IndexSchema>
        {
            new("PK_Users", "CLUSTERED", new List<string> { "Id" }, true, true),
            new("IX_Users_Email", "NONCLUSTERED", new List<string> { "Email" }, true, false)
        };
        
        var constraints = new List<ConstraintSchema>
        {
            new("CK_Users_Email_Format", "CHECK", "[Email] LIKE '%@%.%'"),
            new("FK_Users_Department", "FOREIGN KEY", "REFERENCES Departments (Id)")
        };

        // Act
        var table = new TableSchema("dbo", "Users", columns, indexes, constraints);

        // Assert
        table.Schema.Should().Be("dbo");
        table.Name.Should().Be("Users");
        table.Columns.Should().HaveCount(3);
        table.Indexes.Should().HaveCount(2);
        table.Constraints.Should().HaveCount(2);
        
        // Verify column details
        var idColumn = table.Columns.First(c => c.Name == "Id");
        idColumn.IsPrimaryKey.Should().BeTrue();
        idColumn.IsIdentity.Should().BeTrue();
        idColumn.IsNullable.Should().BeFalse();
        
        var emailColumn = table.Columns.First(c => c.Name == "Email");
        emailColumn.IsPrimaryKey.Should().BeFalse();
        emailColumn.IsIdentity.Should().BeFalse();
        
        var createdColumn = table.Columns.First(c => c.Name == "CreatedDate");
        createdColumn.DefaultValue.Should().Be("GETUTCDATE()");
        
        // Verify index details
        var primaryKey = table.Indexes.First(i => i.Name == "PK_Users");
        primaryKey.IsUnique.Should().BeTrue();
        primaryKey.IsClustered.Should().BeTrue();
        primaryKey.Columns.Should().Contain("Id");
        
        var emailIndex = table.Indexes.First(i => i.Name == "IX_Users_Email");
        emailIndex.IsUnique.Should().BeTrue();
        emailIndex.IsClustered.Should().BeFalse();
        
        // Verify constraint details
        var checkConstraint = table.Constraints.First(c => c.Name == "CK_Users_Email_Format");
        checkConstraint.Type.Should().Be("CHECK");
        checkConstraint.Definition.Should().Contain("Email");
        
        var foreignKey = table.Constraints.First(c => c.Name == "FK_Users_Department");
        foreignKey.Type.Should().Be("FOREIGN KEY");
        foreignKey.Definition.Should().Contain("REFERENCES");
    }

    [Fact]
    public void StoredProcedureSchema_WithParameters_StoresParameterInformation()
    {
        // Arrange
        var parameters = new List<ParameterSchema>
        {
            new("@UserId", "int", false, null),
            new("@IncludeInactive", "bit", false, "0"),
            new("@TotalCount", "int", true, null)
        };

        // Act
        var procedure = new StoredProcedureSchema(
            "dbo", 
            "GetUserDetails", 
            "SELECT * FROM Users WHERE Id = @UserId AND (Active = 1 OR @IncludeInactive = 1)", 
            parameters);

        // Assert
        procedure.Schema.Should().Be("dbo");
        procedure.Name.Should().Be("GetUserDetails");
        procedure.Definition.Should().Contain("SELECT * FROM Users");
        procedure.Parameters.Should().HaveCount(3);
        
        var userIdParam = procedure.Parameters.First(p => p.Name == "@UserId");
        userIdParam.DataType.Should().Be("int");
        userIdParam.IsOutput.Should().BeFalse();
        userIdParam.DefaultValue.Should().BeNull();
        
        var includeInactiveParam = procedure.Parameters.First(p => p.Name == "@IncludeInactive");
        includeInactiveParam.DefaultValue.Should().Be("0");
        
        var totalCountParam = procedure.Parameters.First(p => p.Name == "@TotalCount");
        totalCountParam.IsOutput.Should().BeTrue();
    }

    [Fact]
    public void FunctionSchema_WithReturnTypeAndParameters_StoresCompleteInformation()
    {
        // Arrange
        var parameters = new List<ParameterSchema>
        {
            new("@StartDate", "datetime2", false, null),
            new("@EndDate", "datetime2", false, null)
        };

        // Act
        var function = new FunctionSchema(
            "dbo",
            "GetUserCountByDateRange",
            "RETURN (SELECT COUNT(*) FROM Users WHERE CreatedDate BETWEEN @StartDate AND @EndDate)",
            "int",
            parameters);

        // Assert
        function.Schema.Should().Be("dbo");
        function.Name.Should().Be("GetUserCountByDateRange");
        function.ReturnType.Should().Be("int");
        function.Definition.Should().Contain("RETURN");
        function.Parameters.Should().HaveCount(2);
        
        function.Parameters.All(p => !p.IsOutput).Should().BeTrue("Function parameters are not output parameters");
        function.Parameters.All(p => p.DataType == "datetime2").Should().BeTrue();
    }

    [Fact]
    public void ViewSchema_WithDefinition_StoresViewInformation()
    {
        // Arrange
        var definition = @"
            SELECT 
                u.Id,
                u.Email,
                u.CreatedDate,
                d.Name as DepartmentName
            FROM Users u
            INNER JOIN Departments d ON u.DepartmentId = d.Id
            WHERE u.Active = 1";

        // Act
        var view = new ViewSchema("dbo", "ActiveUsersWithDepartment", definition);

        // Assert
        view.Schema.Should().Be("dbo");
        view.Name.Should().Be("ActiveUsersWithDepartment");
        view.Definition.Should().Contain("SELECT");
        view.Definition.Should().Contain("INNER JOIN");
        view.Definition.Should().Contain("WHERE u.Active = 1");
    }

    #endregion

    #region Git Migration Priority Tests

    [Theory]
    [InlineData("removed", 1)]
    [InlineData("modified", 2)]
    [InlineData("added", 3)]
    [InlineData("unknown", 4)]
    public void GetMigrationPriority_ReturnsCorrectPriority(string changeType, int expectedPriority)
    {
        // Use reflection to test private method
        var plugin = new GitSchemaPlugin();
        var method = typeof(GitSchemaPlugin).GetMethod("GetMigrationPriority", 
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (int)method!.Invoke(plugin, new object[] { changeType });

        // Assert
        result.Should().Be(expectedPriority);
    }

    #endregion

    #region Schema Difference Analysis Tests

    [Fact]
    public void SchemaDifference_ForTableChanges_CapturesCorrectInformation()
    {
        // Arrange & Act
        var addedTable = new SchemaDifference("Added", "Table", "dbo.NewTable", "New table");
        var modifiedTable = new SchemaDifference("Modified", "Table", "dbo.ExistingTable", "Column added");
        var removedTable = new SchemaDifference("Removed", "Table", "dbo.OldTable", "Table dropped");

        // Assert
        addedTable.ChangeType.Should().Be("Added");
        addedTable.ObjectType.Should().Be("Table");
        
        modifiedTable.ChangeType.Should().Be("Modified");
        modifiedTable.Details.Should().Contain("Column added");
        
        removedTable.ChangeType.Should().Be("Removed");
        removedTable.Details.Should().Contain("dropped");
    }

    [Theory]
    [InlineData("Table", "dbo.Users")]
    [InlineData("View", "dbo.ActiveUsers")]
    [InlineData("StoredProcedure", "dbo.GetUserDetails")]
    [InlineData("Function", "dbo.CalculateUserAge")]
    [InlineData("Index", "IX_Users_Email")]
    [InlineData("Constraint", "CK_Users_EmailFormat")]
    public void SchemaDifference_ForDifferentObjectTypes_HandlesCorrectly(string objectType, string objectName)
    {
        // Arrange & Act
        var difference = new SchemaDifference("Modified", objectType, objectName, "Test change");

        // Assert
        difference.ObjectType.Should().Be(objectType);
        difference.ObjectName.Should().Be(objectName);
        difference.ChangeType.Should().Be("Modified");
        difference.Details.Should().Be("Test change");
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public void DatabaseSchema_WithEmptyCollections_HandlesGracefully()
    {
        // Arrange & Act
        var schema = new DatabaseSchema(
            "EmptyDB",
            DateTime.UtcNow,
            new List<TableSchema>(),
            new List<ViewSchema>(),
            new List<StoredProcedureSchema>(),
            new List<FunctionSchema>());

        // Assert
        schema.Tables.Should().BeEmpty();
        schema.Views.Should().BeEmpty();
        schema.StoredProcedures.Should().BeEmpty();
        schema.Functions.Should().BeEmpty();
        schema.DatabaseName.Should().Be("EmptyDB");
    }

    [Fact]
    public void TableSchema_WithNullOrEmptyValues_HandlesDefaults()
    {
        // Arrange
        var columns = new List<ColumnSchema>
        {
            new("TestColumn", "varchar(50)", true, false, false, null)
        };

        // Act
        var table = new TableSchema("", "TestTable", columns, new List<IndexSchema>(), new List<ConstraintSchema>());

        // Assert
        table.Schema.Should().Be("");
        table.Name.Should().Be("TestTable");
        table.Columns.Should().HaveCount(1);
        table.Indexes.Should().BeEmpty();
        table.Constraints.Should().BeEmpty();
        
        var column = table.Columns[0];
        column.DefaultValue.Should().BeNull();
        column.IsNullable.Should().BeTrue();
        column.IsPrimaryKey.Should().BeFalse();
        column.IsIdentity.Should().BeFalse();
    }

    #endregion

    #region Schema Extraction Performance Tests

    [Fact]
    public void DatabaseSchema_WithLargeObjectCounts_HandlesEfficiently()
    {
        // Arrange - Create schema with many objects
        var tables = Enumerable.Range(1, 100)
            .Select(i => new TableSchema($"dbo", $"Table{i}", 
                new List<ColumnSchema> { new($"Id{i}", "int", false, true, true, null) },
                new List<IndexSchema>(),
                new List<ConstraintSchema>()))
            .ToList();

        var views = Enumerable.Range(1, 50)
            .Select(i => new ViewSchema("dbo", $"View{i}", $"SELECT * FROM Table{i}"))
            .ToList();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var schema = new DatabaseSchema("LargeDB", DateTime.UtcNow, tables, views, 
            new List<StoredProcedureSchema>(), new List<FunctionSchema>());
        stopwatch.Stop();

        // Assert
        schema.Tables.Should().HaveCount(100);
        schema.Views.Should().HaveCount(50);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Schema creation should be fast");
    }

    #endregion
}