# Demo: Data Profiling and Analytics

## Overview
This demo showcases the data profiling and analytics capabilities of the Database Automation Platform, including data quality analysis, pattern detection, and anomaly detection.

## Scenario
As a data analyst or quality engineer, you need to:
1. Profile data to understand its characteristics
2. Identify data quality issues
3. Detect patterns and anomalies
4. Generate recommendations for data improvement

## Steps

### 1. Basic Data Profiling
Profile a table to get statistical information about the data.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 1,
  "params": {
    "name": "analyze",
    "arguments": {
      "database": "AdventureWorks",
      "analysisType": "statistics",
      "target": "Sales.SalesOrderDetail"
    }
  }
}
```

**Expected Result:**
```json
{
  "tableName": "Sales.SalesOrderDetail",
  "rowCount": 121317,
  "columns": {
    "OrderQty": {
      "columnName": "OrderQty",
      "dataType": "smallint",
      "distinctCount": 41,
      "nullCount": 0,
      "nullPercentage": 0.0,
      "minValue": 1,
      "maxValue": 44,
      "meanValue": 1.8,
      "medianValue": 1.0,
      "modeValue": 1,
      "standardDeviation": 1.9,
      "topValues": {
        "1": 86974,
        "2": 15884,
        "3": 6046,
        "4": 3784,
        "5": 2166
      }
    },
    "UnitPrice": {
      "columnName": "UnitPrice",
      "dataType": "money",
      "distinctCount": 195,
      "nullCount": 0,
      "nullPercentage": 0.0,
      "minValue": 1.374,
      "maxValue": 3578.27,
      "meanValue": 441.56,
      "distribution": {
        "type": "RightSkewed",
        "skewness": 2.34,
        "kurtosis": 5.67
      }
    }
  },
  "metrics": {
    "dataSizeBytes": 15728640,
    "indexSizeBytes": 5242880,
    "averageRowSizeBytes": 130,
    "columnCount": 9,
    "indexCount": 3,
    "fragmentationPercentage": 12.5
  },
  "profiledAt": "2024-01-15T10:30:00Z"
}
```

### 2. Data Quality Analysis
Analyze data quality based on predefined rules.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 2,
  "params": {
    "name": "analyze_quality",
    "arguments": {
      "database": "AdventureWorks",
      "tableName": "Person.EmailAddress",
      "rules": {
        "checkCompleteness": true,
        "checkConsistency": true,
        "checkValidity": true,
        "checkUniqueness": true,
        "customRules": {
          "EmailFormat": ["^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$"],
          "NoDuplicates": ["EmailAddress"]
        }
      }
    }
  }
}
```

**Expected Result:**
```json
{
  "overallQualityScore": 0.87,
  "issues": [
    {
      "type": "Invalid Format",
      "severity": "Medium",
      "column": "EmailAddress",
      "description": "156 email addresses have invalid format",
      "affectedRows": 156,
      "sampleQuery": "SELECT * FROM Person.EmailAddress WHERE EmailAddress NOT LIKE '%@%.%'"
    },
    {
      "type": "Duplicate Values",
      "severity": "High",
      "column": "EmailAddress",
      "description": "Found 23 duplicate email addresses",
      "affectedRows": 46,
      "sampleQuery": "SELECT EmailAddress, COUNT(*) FROM Person.EmailAddress GROUP BY EmailAddress HAVING COUNT(*) > 1"
    },
    {
      "type": "Missing Values",
      "severity": "Low",
      "column": "ModifiedDate",
      "description": "12 rows have NULL ModifiedDate",
      "affectedRows": 12,
      "sampleQuery": "SELECT * FROM Person.EmailAddress WHERE ModifiedDate IS NULL"
    }
  ],
  "columnMetrics": {
    "EmailAddress": {
      "completenessScore": 1.0,
      "consistencyScore": 0.95,
      "validityScore": 0.85,
      "uniquenessScore": 0.98,
      "accuracyScore": 0.90
    }
  },
  "recommendations": [
    "Implement email validation before inserting records",
    "Add unique constraint on EmailAddress column",
    "Set default value for ModifiedDate column"
  ]
}
```

### 3. Pattern Detection
Detect patterns in data values.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 3,
  "params": {
    "name": "detect_patterns",
    "arguments": {
      "database": "AdventureWorks",
      "tableName": "Person.PersonPhone",
      "columnName": "PhoneNumber"
    }
  }
}
```

**Expected Result:**
```json
{
  "columnName": "PhoneNumber",
  "detectedPatterns": [
    {
      "name": "US Standard",
      "regexPattern": "^\\(\\d{3}\\) \\d{3}-\\d{4}$",
      "matchPercentage": 72.5,
      "examples": ["(425) 555-0123", "(206) 555-0198", "(360) 555-0145"],
      "description": "US phone number format with area code"
    },
    {
      "name": "International",
      "regexPattern": "^\\+\\d{1,3}-\\d{2,4}-\\d{3}-\\d{4}$",
      "matchPercentage": 15.3,
      "examples": ["+1-425-555-0123", "+44-20-555-0198", "+81-3-555-0145"],
      "description": "International format with country code"
    },
    {
      "name": "Simple Numeric",
      "regexPattern": "^\\d{10}$",
      "matchPercentage": 8.7,
      "examples": ["4255550123", "2065550198", "3605550145"],
      "description": "10-digit number without formatting"
    }
  ],
  "typeRecommendations": [
    {
      "currentType": "nvarchar(25)",
      "recommendedType": "varchar(20)",
      "reason": "No Unicode characters detected, can save storage",
      "confidenceScore": 0.98,
      "spaceSavingsBytes": 125000
    }
  ]
}
```

### 4. Anomaly Detection
Detect anomalies in data.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 4,
  "params": {
    "name": "detect_anomalies",
    "arguments": {
      "database": "AdventureWorks",
      "tableName": "Sales.SalesOrderHeader",
      "options": {
        "method": "IsolationForest",
        "contaminationRate": 0.05,
        "specificColumns": ["TotalDue", "SubTotal", "TaxAmt"],
        "startDate": "2023-01-01",
        "endDate": "2023-12-31"
      }
    }
  }
}
```

**Expected Result:**
```json
{
  "anomalies": [
    {
      "type": "Unusual High Value",
      "description": "Order total significantly higher than normal",
      "anomalyScore": 0.92,
      "detectedAt": "2023-08-15T14:30:00Z",
      "context": {
        "SalesOrderID": 75123,
        "TotalDue": 183745.98,
        "AverageOrderValue": 3521.45,
        "StandardDeviations": 4.2
      },
      "query": "SELECT * FROM Sales.SalesOrderHeader WHERE SalesOrderID = 75123"
    },
    {
      "type": "Pattern Anomaly",
      "description": "Unusual combination of subtotal and tax amount",
      "anomalyScore": 0.87,
      "detectedAt": "2023-11-22T09:15:00Z",
      "context": {
        "SalesOrderID": 76890,
        "SubTotal": 5000.00,
        "TaxAmt": 0.00,
        "ExpectedTaxRate": 0.08
      },
      "query": "SELECT * FROM Sales.SalesOrderHeader WHERE SalesOrderID = 76890"
    }
  ],
  "statistics": {
    "totalAnomalies": 152,
    "anomaliesByType": {
      "Unusual High Value": 45,
      "Pattern Anomaly": 67,
      "Time Series Anomaly": 40
    },
    "anomalyRate": 0.048,
    "firstAnomaly": "2023-01-15T10:30:00Z",
    "lastAnomaly": "2023-12-28T16:45:00Z"
  },
  "affectedColumns": ["TotalDue", "SubTotal", "TaxAmt"]
}
```

### 5. Generate Data Insights
Generate comprehensive insights from query results.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 5,
  "params": {
    "name": "generate_insights",
    "arguments": {
      "database": "AdventureWorks",
      "query": "SELECT ProductID, OrderQty, UnitPrice, LineTotal FROM Sales.SalesOrderDetail WHERE OrderDate >= '2023-01-01'",
      "options": {
        "includeCorrelations": true,
        "includeTrends": true,
        "includeOutliers": true,
        "correlationThreshold": 0.7
      }
    }
  }
}
```

## Key Features Demonstrated

1. **Data Profiling**: Statistical analysis of data characteristics
2. **Quality Analysis**: Identify data quality issues
3. **Pattern Detection**: Find common patterns in data
4. **Anomaly Detection**: Identify unusual data points
5. **Insight Generation**: Discover correlations and trends

## Best Practices

1. Profile data regularly to track quality over time
2. Define custom quality rules based on business requirements
3. Investigate anomalies promptly
4. Use pattern detection to standardize data formats
5. Monitor data quality metrics in dashboards
6. Act on recommendations to improve data quality

## Use Cases

1. **Data Migration**: Profile source data before migration
2. **Quality Assurance**: Regular data quality checks
3. **Fraud Detection**: Identify unusual patterns
4. **Performance Optimization**: Find data distribution issues
5. **Compliance**: Ensure data meets regulatory standards