using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlServerMcpFunctions.Domain.Entities;
using SqlServerMcpFunctions.Domain.ValueObjects;

namespace SqlServerMcpFunctions.Application.Services
{
    /// <summary>
    /// Helper methods for McpServerService
    /// </summary>
    public partial class McpServerService
    {
        /// <summary>
        /// Validate tool parameters against the tool's JSON schema
        /// </summary>
        private async Task ValidateToolParametersAsync(McpTool tool, Dictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            // TODO: Implement JSON schema validation
            // For now, we'll do basic null checks and type validation
            
            if (parameters == null)
            {
                parameters = new Dictionary<string, object?>();
            }

            // Get stored procedure parameter metadata for validation
            var procedureParams = await _storedProcedureExecutor.GetParameterMetadataAsync(
                tool.DatabaseIdentifier, 
                tool.StoredProcedureName, 
                cancellationToken);

            foreach (var param in procedureParams.Where(p => p.IsInput && !p.IsNullable))
            {
                if (!parameters.ContainsKey(param.Name))
                {
                    throw new ArgumentException($"Required parameter '{param.Name}' is missing");
                }

                if (parameters[param.Name] == null)
                {
                    throw new ArgumentException($"Required parameter '{param.Name}' cannot be null");
                }
            }

            _logger.LogDebug("Parameter validation completed for tool: {ToolName}", tool.Name);
        }

        /// <summary>
        /// Convert stored procedure result to MCP-compatible format
        /// </summary>
        private object ConvertResultToMcpFormat(StoredProcedureResult result)
        {
            var mcpResult = new
            {
                resultSets = result.ResultSets.Select((rs, index) => new
                {
                    index = index,
                    columnCount = rs.Columns.Count,
                    rowCount = rs.Rows.Count,
                    columns = rs.Columns.Cast<System.Data.DataColumn>().Select(col => new
                    {
                        name = col.ColumnName,
                        dataType = col.DataType.Name,
                        allowDBNull = col.AllowDBNull
                    }).ToArray(),
                    rows = rs.AsEnumerable().Select(row => 
                        rs.Columns.Cast<System.Data.DataColumn>()
                            .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToArray()
                }).ToArray(),
                outputParameters = result.OutputParameters,
                returnValue = result.ReturnValue,
                rowsAffected = result.RowsAffected,
                executionTimeMs = result.ExecutionTimeMs
            };

            return mcpResult;
        }

        /// <summary>
        /// Create an error response for failed requests
        /// </summary>
        private McpMessage CreateErrorResponse(string? requestId, int errorCode, string errorMessage)
        {
            return new McpResponse<object>
            {
                Id = requestId,
                Error = new McpError
                {
                    Code = errorCode,
                    Message = errorMessage
                }
            };
        }
