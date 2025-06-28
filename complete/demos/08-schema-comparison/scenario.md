# Demo: Schema Comparison

## Overview
This demo shows how to compare database schemas and generate migration scripts using the Database Automation Platform.

## Scenario
As a database administrator or developer, you need to:
1. Compare schemas between development and production databases
2. Identify differences in tables, columns, indexes, and procedures
3. Generate migration scripts to synchronize schemas
4. Validate changes before deployment

## Steps

### 1. Get Schema Information for Source Database
First, retrieve the schema information for the source database.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 1,
  "params": {
    "name": "schema",
    "arguments": {
      "database": "AdventureWorks_Dev",
      "objectType": "Table",
      "objectName": "Sales.Customer"
    }
  }
}
```

### 2. Compare Schemas Between Databases
Use the schema service to compare two databases.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 2,
  "params": {
    "name": "schema_compare",
    "arguments": {
      "sourceDatabase": "AdventureWorks_Dev",
      "targetDatabase": "AdventureWorks_Prod",
      "options": {
        "includeTables": true,
        "includeViews": true,
        "includeProcedures": true,
        "includeIndexes": true,
        "ignoreWhitespace": true
      }
    }
  }
}
```

**Expected Result:**
```json
{
  "differences": [
    {
      "type": "Added",
      "objectType": "Column",
      "objectName": "Sales.Customer.LoyaltyPoints",
      "sourceDefinition": "INT NULL DEFAULT 0",
      "targetDefinition": null,
      "description": "Column exists in Dev but not in Prod"
    },
    {
      "type": "Modified",
      "objectType": "Index",
      "objectName": "IX_Customer_TerritoryID",
      "sourceDefinition": "NONCLUSTERED INDEX ON Sales.Customer(TerritoryID) INCLUDE (PersonID)",
      "targetDefinition": "NONCLUSTERED INDEX ON Sales.Customer(TerritoryID)",
      "description": "Index has different included columns"
    },
    {
      "type": "Removed",
      "objectType": "Procedure",
      "objectName": "Sales.uspGetCustomerOrders_Old",
      "sourceDefinition": null,
      "targetDefinition": "CREATE PROCEDURE Sales.uspGetCustomerOrders_Old...",
      "description": "Procedure exists in Prod but not in Dev"
    }
  ],
  "totalDifferences": 3,
  "differenceCounts": {
    "Added": 1,
    "Modified": 1,
    "Removed": 1
  }
}
```

### 3. Generate Migration Script
Generate a migration script to synchronize the schemas.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 3,
  "params": {
    "name": "schema_migrate",
    "arguments": {
      "sourceDatabase": "AdventureWorks_Dev",
      "targetDatabase": "AdventureWorks_Prod",
      "options": {
        "includeDropStatements": false,
        "preserveData": true,
        "generateRollback": true,
        "useTransactions": true
      }
    }
  }
}
```

**Expected Result:**
```json
{
  "script": "-- Migration script generated on 2024-01-15\n-- Source: AdventureWorks_Dev\n-- Target: AdventureWorks_Prod\n\nBEGIN TRANSACTION;\n\n-- Add new column\nALTER TABLE Sales.Customer\nADD LoyaltyPoints INT NULL DEFAULT 0;\n\n-- Modify index\nDROP INDEX IX_Customer_TerritoryID ON Sales.Customer;\nCREATE NONCLUSTERED INDEX IX_Customer_TerritoryID\nON Sales.Customer(TerritoryID)\nINCLUDE (PersonID);\n\n-- Drop old procedure\nIF EXISTS (SELECT * FROM sys.procedures WHERE name = 'uspGetCustomerOrders_Old')\n    DROP PROCEDURE Sales.uspGetCustomerOrders_Old;\n\nCOMMIT TRANSACTION;",
  "steps": [
    {
      "order": 1,
      "description": "Add LoyaltyPoints column to Sales.Customer",
      "script": "ALTER TABLE Sales.Customer ADD LoyaltyPoints INT NULL DEFAULT 0;",
      "isReversible": true,
      "rollbackScript": "ALTER TABLE Sales.Customer DROP COLUMN LoyaltyPoints;"
    },
    {
      "order": 2,
      "description": "Recreate IX_Customer_TerritoryID with included columns",
      "script": "DROP INDEX IX_Customer_TerritoryID ON Sales.Customer;\nCREATE NONCLUSTERED INDEX IX_Customer_TerritoryID ON Sales.Customer(TerritoryID) INCLUDE (PersonID);",
      "isReversible": true,
      "rollbackScript": "DROP INDEX IX_Customer_TerritoryID ON Sales.Customer;\nCREATE NONCLUSTERED INDEX IX_Customer_TerritoryID ON Sales.Customer(TerritoryID);"
    }
  ],
  "warnings": [
    "Dropping stored procedure 'uspGetCustomerOrders_Old' - ensure it's no longer in use",
    "Index recreation may take time on large tables"
  ],
  "requiresDataMigration": false,
  "estimatedDurationSeconds": 45
}
```

### 4. Validate Schema Changes
Validate the migration script before execution.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 4,
  "params": {
    "name": "schema_validate",
    "arguments": {
      "database": "AdventureWorks_Prod",
      "rules": {
        "checkNamingConventions": true,
        "checkDataTypes": true,
        "checkIndexes": true,
        "checkConstraints": true
      }
    }
  }
}
```

## Key Features Demonstrated

1. **Schema Comparison**: Identify differences between databases
2. **Difference Types**: Added, Modified, and Removed objects
3. **Migration Generation**: Create scripts to synchronize schemas
4. **Rollback Scripts**: Generate reverse operations
5. **Validation**: Check schemas against best practices

## Best Practices

1. Always compare schemas before deployment
2. Review all differences carefully
3. Test migration scripts in a non-production environment
4. Generate and save rollback scripts
5. Use transactions for atomic changes
6. Validate schemas after migration

## Common Use Cases

1. **Development to Production**: Promote changes from dev to prod
2. **Environment Sync**: Keep multiple environments in sync
3. **Audit Changes**: Track schema evolution over time
4. **Disaster Recovery**: Document schema for rebuilding
5. **Compliance**: Ensure schema standards are met