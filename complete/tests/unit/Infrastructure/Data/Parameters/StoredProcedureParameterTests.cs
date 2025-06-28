using System;
using System.Data;
using System.Data.Common;
using Xunit;
using FluentAssertions;
using Moq;
using DatabaseAutomationPlatform.Infrastructure.Data.Parameters;

namespace DatabaseAutomationPlatform.Infrastructure.Tests.Unit.Data.Parameters
{
    public class StoredProcedureParameterTests
    {
        [Fact]
        public void Constructor_WithValidInputs_CreatesParameter()
        {
            // Arrange
            var name = "@TestParam";
            var value = "TestValue";
            var sqlDbType = SqlDbType.NVarChar;
            
            // Act
            var parameter = new StoredProcedureParameter(
                name, value, sqlDbType, ParameterDirection.Input, 50, null, null, false);
            
            // Assert
            parameter.Name.Should().Be(name);
            parameter.Value.Should().Be(value);
            parameter.SqlDbType.Should().Be(sqlDbType);
            parameter.Direction.Should().Be(ParameterDirection.Input);
            parameter.Size.Should().Be(50);
            parameter.IsSensitive.Should().BeFalse();
        }
        [Fact]
        public void Constructor_WithNameWithoutAtSign_AddsAtSign()
        {
            // Arrange & Act
            var parameter = new StoredProcedureParameter("TestParam", "value", SqlDbType.NVarChar);
            
            // Assert
            parameter.Name.Should().Be("@TestParam");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithInvalidName_ThrowsArgumentException(string invalidName)
        {
            // Arrange & Act
            var act = () => new StoredProcedureParameter(invalidName, "value", SqlDbType.NVarChar);
            
            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Parameter name cannot be null or empty*")
                .And.ParamName.Should().Be("name");
        }

        [Fact]
        public void Input_CreatesInputParameter()
        {
            // Arrange & Act
            var parameter = StoredProcedureParameter.Input("@TestParam", "TestValue", SqlDbType.NVarChar, true);
            
            // Assert
            parameter.Direction.Should().Be(ParameterDirection.Input);
            parameter.IsSensitive.Should().BeTrue();
        }
        [Fact]
        public void Output_CreatesOutputParameter()
        {
            // Arrange & Act
            var parameter = StoredProcedureParameter.Output("@OutputParam", SqlDbType.Int, 4);
            
            // Assert
            parameter.Direction.Should().Be(ParameterDirection.Output);
            parameter.Value.Should().BeNull();
            parameter.Size.Should().Be(4);
        }

        [Fact]
        public void ReturnValue_CreatesReturnValueParameter()
        {
            // Arrange & Act
            var parameter = StoredProcedureParameter.ReturnValue();
            
            // Assert
            parameter.Name.Should().Be("@ReturnValue");
            parameter.Direction.Should().Be(ParameterDirection.ReturnValue);
            parameter.SqlDbType.Should().Be(SqlDbType.Int);
            parameter.Value.Should().BeNull();
        }

        [Fact]
        public void ApplyToCommand_WithValidCommand_AddsParameter()
        {
            // Arrange
            var mockCommand = new Mock<DbCommand>();
            var mockParameter = new Mock<DbParameter>();
            mockCommand.Setup(c => c.CreateParameter()).Returns(mockParameter.Object);
            mockCommand.Setup(c => c.Parameters.Add(It.IsAny<DbParameter>())).Verifiable();
            
            var parameter = new StoredProcedureParameter("@TestParam", "TestValue", SqlDbType.NVarChar);
            
            // Act
            parameter.ApplyToCommand(mockCommand.Object);
            
            // Assert
            mockParameter.VerifySet(p => p.ParameterName = "@TestParam");
            mockParameter.VerifySet(p => p.Value = "TestValue");
            mockParameter.VerifySet(p => p.Direction = ParameterDirection.Input);
            mockCommand.Verify(c => c.Parameters.Add(mockParameter.Object), Times.Once);
        }
        [Fact]
        public void ApplyToCommand_WithNullValue_SetsDBNull()
        {
            // Arrange
            var mockCommand = new Mock<DbCommand>();
            var mockParameter = new Mock<DbParameter>();
            mockCommand.Setup(c => c.CreateParameter()).Returns(mockParameter.Object);
            
            var parameter = new StoredProcedureParameter("@TestParam", null, SqlDbType.NVarChar);
            
            // Act
            parameter.ApplyToCommand(mockCommand.Object);
            
            // Assert
            mockParameter.VerifySet(p => p.Value = DBNull.Value);
        }

        [Fact]
        public void ApplyToCommand_WithNullCommand_ThrowsArgumentNullException()
        {
            // Arrange
            var parameter = new StoredProcedureParameter("@TestParam", "value", SqlDbType.NVarChar);
            
            // Act
            var act = () => parameter.ApplyToCommand(null!);
            
            // Assert
            act.Should().Throw<ArgumentNullException>()
                .And.ParamName.Should().Be("command");
        }

        [Fact]
        public void ToLogString_WithNonSensitiveValue_ReturnsFullDetails()
        {
            // Arrange
            var parameter = new StoredProcedureParameter("@TestParam", "TestValue", SqlDbType.NVarChar, isSensitive: false);
            
            // Act
            var logString = parameter.ToLogString();
            
            // Assert
            logString.Should().Be("@TestParam=TestValue (Type: NVarChar, Direction: Input)");
        }

        [Fact]
        public void ToLogString_WithSensitiveValue_MasksValue()
        {
            // Arrange
            var parameter = new StoredProcedureParameter("@Password", "SecretPassword123", SqlDbType.NVarChar, isSensitive: true);
            
            // Act
            var logString = parameter.ToLogString();
            
            // Assert
            logString.Should().Be("@Password=***REDACTED*** (Type: NVarChar, Direction: Input)");
        }

        [Fact]
        public void ToLogString_WithNullValue_ShowsNULL()
        {
            // Arrange
            var parameter = new StoredProcedureParameter("@TestParam", null, SqlDbType.Int);
            
            // Act
            var logString = parameter.ToLogString();
            
            // Assert
            logString.Should().Be("@TestParam=NULL (Type: Int, Direction: Input)");
        }
    }
}