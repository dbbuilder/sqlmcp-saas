using System;
using System.Collections.Generic;
using SqlMcp.Core.Auditing.Models;
using Xunit;
using FluentAssertions;

namespace SqlMcp.Tests.Unit.Core.Auditing.Models
{
    /// <summary>
    /// Unit tests for database audit events
    /// </summary>
    public class DatabaseAuditEventTests
    {
        [Fact]
        public void DatabaseAuditEvent_Constructor_ShouldInitializeProperties()
        {
            // Arrange
            var operation = DatabaseOperation.Update;
            var entityName = "User";
            var entityId = "123";
            var userId = "user456";
            var correlationId = "corr-789";

            // Act
            var auditEvent = new DatabaseAuditEvent(
                operation, 
                entityName, 
                entityId, 
                userId, 
                correlationId);

            // Assert
            auditEvent.Operation.Should().Be(operation);
            auditEvent.EntityName.Should().Be(entityName);
            auditEvent.EntityId.Should().Be(entityId);
            auditEvent.EventType.Should().Be("Database.Update");
            auditEvent.UserId.Should().Be(userId);
            auditEvent.CorrelationId.Should().Be(correlationId);
        }

        [Theory]
        [InlineData(DatabaseOperation.Create, "Database.Create")]
        [InlineData(DatabaseOperation.Read, "Database.Read")]
        [InlineData(DatabaseOperation.Update, "Database.Update")]
        [InlineData(DatabaseOperation.Delete, "Database.Delete")]
        [InlineData(DatabaseOperation.Execute, "Database.Execute")]
        public void DatabaseAuditEvent_EventType_ShouldReflectOperation(
            DatabaseOperation operation, 
            string expectedEventType)
        {
            // Arrange & Act
            var auditEvent = new DatabaseAuditEvent(operation, "TestEntity", "1");

            // Assert
            auditEvent.EventType.Should().Be(expectedEventType);
        }

        [Fact]
        public void DatabaseAuditEvent_WithStoredProcedure_ShouldSetProperties()
        {
            // Arrange
            var spName = "sp_UpdateUser";
            var parameters = new Dictionary<string, object>
            {
                ["UserId"] = 123,
                ["Email"] = "test@example.com"
            };

            // Act
            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Execute, 
                "StoredProcedure", 
                spName)
            {
                StoredProcedureName = spName,
                Parameters = parameters
            };

            // Assert
            auditEvent.StoredProcedureName.Should().Be(spName);
            auditEvent.Parameters.Should().BeEquivalentTo(parameters);
        }

        [Fact]
        public void DatabaseAuditEvent_WithBeforeAfterData_ShouldStoreCorrectly()
        {
            // Arrange
            var beforeData = new Dictionary<string, object>
            {
                ["Name"] = "Old Name",
                ["Email"] = "old@example.com"
            };
            var afterData = new Dictionary<string, object>
            {
                ["Name"] = "New Name",
                ["Email"] = "new@example.com"
            };

            // Act
            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Update, 
                "User", 
                "123")
            {
                BeforeData = beforeData,
                AfterData = afterData
            };

            // Assert
            auditEvent.BeforeData.Should().BeEquivalentTo(beforeData);
            auditEvent.AfterData.Should().BeEquivalentTo(afterData);
        }

        [Fact]
        public void DatabaseAuditEvent_WithExecutionMetrics_ShouldStoreCorrectly()
        {
            // Arrange
            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Execute, 
                "StoredProcedure", 
                "sp_GetUsers");

            // Act
            auditEvent.ExecutionTimeMs = 125;
            auditEvent.RowsAffected = 42;
            auditEvent.Success = true;

            // Assert
            auditEvent.ExecutionTimeMs.Should().Be(125);
            auditEvent.RowsAffected.Should().Be(42);
            auditEvent.Success.Should().BeTrue();
        }

        [Fact]
        public void DatabaseAuditEvent_WithError_ShouldCaptureDetails()
        {
            // Arrange
            var errorMessage = "Violation of UNIQUE KEY constraint";
            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Insert, 
                "User", 
                "new-user");

            // Act
            auditEvent.Success = false;
            auditEvent.ErrorMessage = errorMessage;

            // Assert
            auditEvent.Success.Should().BeFalse();
            auditEvent.ErrorMessage.Should().Be(errorMessage);
            auditEvent.Severity.Should().Be(AuditSeverity.Error);
        }

        [Fact]
        public void DatabaseAuditEvent_GetChangedFields_ShouldIdentifyChanges()
        {
            // Arrange
            var beforeData = new Dictionary<string, object>
            {
                ["Name"] = "John",
                ["Email"] = "john@example.com",
                ["Age"] = 30
            };
            var afterData = new Dictionary<string, object>
            {
                ["Name"] = "John",
                ["Email"] = "john.doe@example.com",
                ["Age"] = 31
            };

            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Update, 
                "User", 
                "123")
            {
                BeforeData = beforeData,
                AfterData = afterData
            };

            // Act
            var changedFields = auditEvent.GetChangedFields();

            // Assert
            changedFields.Should().HaveCount(2);
            changedFields.Should().Contain(f => f.FieldName == "Email" 
                && f.OldValue.ToString() == "john@example.com" 
                && f.NewValue.ToString() == "john.doe@example.com");
            changedFields.Should().Contain(f => f.FieldName == "Age" 
                && (int)f.OldValue == 30 
                && (int)f.NewValue == 31);
        }

        [Fact]
        public void DatabaseAuditEvent_ToLogString_ShouldIncludeDatabaseDetails()
        {
            // Arrange
            var auditEvent = new DatabaseAuditEvent(
                DatabaseOperation.Update, 
                "User", 
                "123")
            {
                StoredProcedureName = "sp_UpdateUser",
                ExecutionTimeMs = 50,
                RowsAffected = 1,
                Success = true
            };

            // Act
            var logString = auditEvent.ToLogString();

            // Assert
            logString.Should().Contain("Database.Update");
            logString.Should().Contain("Entity=User");
            logString.Should().Contain("EntityId=123");
            logString.Should().Contain("StoredProcedure=sp_UpdateUser");
            logString.Should().Contain("ExecutionTime=50ms");
            logString.Should().Contain("RowsAffected=1");
        }

        [Fact]
        public void DatabaseAuditEvent_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new DatabaseAuditEvent(
                DatabaseOperation.Update, 
                "User", 
                "123")
            {
                BeforeData = new Dictionary<string, object> { ["Name"] = "Old" },
                AfterData = new Dictionary<string, object> { ["Name"] = "New" },
                Parameters = new Dictionary<string, object> { ["Id"] = 123 },
                ExecutionTimeMs = 100
            };

            // Act
            var clone = original.Clone() as DatabaseAuditEvent;

            // Assert
            clone.Should().NotBeNull();
            clone.Should().NotBeSameAs(original);
            clone.Operation.Should().Be(original.Operation);
            clone.EntityName.Should().Be(original.EntityName);
            clone.BeforeData.Should().NotBeSameAs(original.BeforeData);
            clone.BeforeData.Should().BeEquivalentTo(original.BeforeData);
            clone.Parameters.Should().NotBeSameAs(original.Parameters);
        }
    }
}
