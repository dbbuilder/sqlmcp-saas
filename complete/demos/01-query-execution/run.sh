#!/bin/bash

# Query Execution Demo Script
# This script demonstrates how to execute SQL queries using the MCP protocol

# Check if environment variables are set
if [ -z "$API_URL" ] || [ -z "$AUTH_TOKEN" ]; then
    echo "Error: Please set API_URL and AUTH_TOKEN environment variables"
    echo "Example:"
    echo "  export API_URL='http://localhost:5000'"
    echo "  export AUTH_TOKEN='your-jwt-token'"
    exit 1
fi

echo "=== Query Execution Demo ==="
echo "API URL: $API_URL"
echo ""

# Function to execute a query and format output
execute_query() {
    local request_file=$1
    local description=$2
    
    echo "--- $description ---"
    echo "Request:"
    cat "$request_file" | jq '.'
    echo ""
    echo "Response:"
    
    response=$(curl -s -X POST "$API_URL/api/v1/mcp" \
        -H "Authorization: Bearer $AUTH_TOKEN" \
        -H "Content-Type: application/json" \
        -d @"$request_file")
    
    echo "$response" | jq '.'
    echo ""
    
    # Extract execution time if available
    exec_time=$(echo "$response" | jq -r '.result.content.executionTimeMs // "N/A"')
    row_count=$(echo "$response" | jq -r '.result.content.rowCount // "N/A"')
    
    echo "Execution Time: ${exec_time}ms"
    echo "Row Count: $row_count"
    echo ""
}

# Execute the main query
execute_query "request.json" "Basic Query Execution"

# Create and execute a filtering query
cat > filter-request.json << EOF
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 2,
  "params": {
    "name": "query",
    "arguments": {
      "database": "AdventureWorks",
      "query": "SELECT COUNT(*) as TotalCustomers, COUNT(DISTINCT TerritoryID) as Territories FROM Sales.Customer",
      "timeout": 30
    }
  }
}
EOF

execute_query "filter-request.json" "Aggregate Query"

# Create and execute a join query
cat > join-request.json << EOF
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "id": 3,
  "params": {
    "name": "query",
    "arguments": {
      "database": "AdventureWorks",
      "query": "SELECT TOP 5 c.CustomerID, p.FirstName + ' ' + p.LastName as CustomerName FROM Sales.Customer c LEFT JOIN Person.Person p ON c.PersonID = p.BusinessEntityID WHERE c.PersonID IS NOT NULL",
      "timeout": 30
    }
  }
}
EOF

execute_query "join-request.json" "Join Query"

# Clean up temporary files
rm -f filter-request.json join-request.json

echo "=== Demo Complete ===