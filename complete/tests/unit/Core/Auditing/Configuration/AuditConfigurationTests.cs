using System;
using System.Collections.Generic;
using SqlMcp.Core.Auditing.Configuration;
using SqlMcp.Core.Auditing.Models;
using Xunit;
using FluentAssertions;

namespace SqlMcp.Tests.Unit.Core.Auditing.Configuration
{
    /// <summary>
    /// Unit tests for audit configuration
    /// </summary>
    public class AuditConfigurationTests
    {
        [Fact]
        public void AuditConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var config = new AuditConfiguration();

            // Assert
            config.Enabled.Should().BeTrue();
            config.Level.Should().Be(AuditLevel.Detailed);
            config.BufferSize.Should().Be(1000);
            config.FlushIntervalSeconds.Should().Be(5);
            config.RetentionDays.Should().Be(90);
            config.EnableCircuitBreaker.Should().BeTrue();
            config.CircuitBreakerThreshold.Should().Be(5);
            config.CircuitBreakerTimeoutSeconds.Should().Be(60);
        }

        [Fact]
        public void AuditConfiguration_IsValid_WithValidConfig_ShouldReturnTrue()
        {
            // Arrange
            var config = new AuditConfiguration
            {
                BufferSize = 500,
                FlushIntervalSeconds = 10,
                RetentionDays = 30
            };

            // Act
            var isValid = config.IsValid(out var errors);

            // Assert
            isValid.Should().BeTrue();
            errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(100001)]
        public void AuditConfiguration_IsValid_WithInvalidBufferSize_ShouldReturnFalse(int bufferSize)
        {
            // Arrange
            var config = new AuditConfiguration { BufferSize = bufferSize };

            // Act
            var isValid = config.IsValid(out var errors);

            // Assert
            isValid.Should().BeFalse();
            errors.Should().ContainMatch("*Buffer size*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(3601)]
        public void AuditConfiguration_IsValid_WithInvalidFlushInterval_ShouldReturnFalse(int interval)
        {
            // Arrange
            var config = new AuditConfiguration { FlushIntervalSeconds = interval };

            // Act
            var isValid = config.IsValid(out var errors);

            // Assert
            isValid.Should().BeFalse();
            errors.Should().ContainMatch("*Flush interval*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(36501)]
        public void AuditConfiguration_IsValid_WithInvalidRetentionDays_ShouldReturnFalse(int days)
        {
            // Arrange
            var config = new AuditConfiguration { RetentionDays = days };

            // Act
            var isValid = config.IsValid(out var errors);

            // Assert
            isValid.Should().BeFalse();
            errors.Should().ContainMatch("*Retention days*");
        }

        [Fact]
        public void AuditConfiguration_EntityConfigurations_ShouldBeAccessible()
        {
            // Arrange
            var config = new AuditConfiguration();
            var entityConfig = new EntityAuditConfiguration
            {
                EntityName = "User",
                Level = AuditLevel.Verbose,
                IncludeData = true
            };

            // Act
            config.EntityConfigurations["User"] = entityConfig;

            // Assert
            config.EntityConfigurations.Should().ContainKey("User");
            config.EntityConfigurations["User"].Level.Should().Be(AuditLevel.Verbose);
        }

        [Fact]
        public void AuditConfiguration_GetEffectiveLevel_WithEntityOverride_ShouldReturnEntityLevel()
        {
            // Arrange
            var config = new AuditConfiguration
            {
                Level = AuditLevel.Basic
            };
            config.EntityConfigurations["User"] = new EntityAuditConfiguration
            {
                EntityName = "User",
                Level = AuditLevel.Verbose
            };

            // Act
            var level = config.GetEffectiveLevel("User");

            // Assert
            level.Should().Be(AuditLevel.Verbose);
        }

        [Fact]
        public void AuditConfiguration_GetEffectiveLevel_WithoutOverride_ShouldReturnGlobalLevel()
        {
            // Arrange
            var config = new AuditConfiguration
            {
                Level = AuditLevel.Detailed
            };

            // Act
            var level = config.GetEffectiveLevel("Product");

            // Assert
            level.Should().Be(AuditLevel.Detailed);
        }

        [Fact]
        public void AuditConfiguration_ExcludedOperations_ShouldBeConfigurable()
        {
            // Arrange
            var config = new AuditConfiguration();

            // Act
            config.ExcludedOperations.Add("HealthCheck");
            config.ExcludedOperations.Add("Ping");

            // Assert
            config.IsOperationExcluded("HealthCheck").Should().BeTrue();
            config.IsOperationExcluded("Ping").Should().BeTrue();
            config.IsOperationExcluded("UpdateUser").Should().BeFalse();
        }

        [Fact]
        public void AuditConfiguration_SamplingRates_ShouldBeConfigurable()
        {
            // Arrange
            var config = new AuditConfiguration();

            // Act
            config.SamplingRates[DatabaseOperation.Read] = 0.1; // 10% sampling
            config.SamplingRates[DatabaseOperation.Update] = 1.0; // 100% sampling

            // Assert
            config.GetSamplingRate(DatabaseOperation.Read).Should().Be(0.1);
            config.GetSamplingRate(DatabaseOperation.Update).Should().Be(1.0);
            config.GetSamplingRate(DatabaseOperation.Delete).Should().Be(1.0); // Default
        }

        [Fact]
        public void AuditConfiguration_Clone_ShouldCreateDeepCopy()
        {
            // Arrange
            var original = new AuditConfiguration
            {
                Level = AuditLevel.Verbose,
                BufferSize = 2000
            };
            original.EntityConfigurations["User"] = new EntityAuditConfiguration
            {
                Level = AuditLevel.Basic
            };
            original.ExcludedOperations.Add("Test");

            // Act
            var clone = original.Clone();

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.Level.Should().Be(original.Level);
            clone.BufferSize.Should().Be(original.BufferSize);
            clone.EntityConfigurations.Should().NotBeSameAs(original.EntityConfigurations);
            clone.EntityConfigurations.Should().ContainKey("User");
            clone.ExcludedOperations.Should().NotBeSameAs(original.ExcludedOperations);
            clone.ExcludedOperations.Should().Contain("Test");
        }
    }
}
