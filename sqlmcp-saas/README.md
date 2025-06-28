# SQLMCP SaaS Platform

A SaaS platform that provides SQL Server connectivity to AI models via the Model Context Protocol (MCP).

## Quick Start

```bash
# Install dependencies
npm install

# Set up environment variables
cp .env.example .env.local

# Set up database
npx prisma db push

# Run development server
npm run dev
```

## Project Structure

```
sqlmcp-saas/
├── src/
│   ├── components/        # React components
│   │   └── LandingPage.tsx
│   ├── pages/            # Next.js pages
│   ├── mcp-server/       # MCP server implementation
│   │   └── SimpleMcpServer.ts
│   └── services/         # Business logic
├── prisma/
│   └── schema.prisma     # Database schema
├── docs/
│   ├── SIMPLE_MCP_ARCHITECTURE.md
│   └── IMPLEMENTATION_ROADMAP.md
└── public/              # Static assets
```

## Features

### MVP (Complete)
- ✅ Modern SaaS landing page
- ✅ Pricing tiers (Free/Starter/Pro/Enterprise)
- ✅ Simple MCP server with SQL connectivity
- ✅ Authentication system design
- ✅ Database schema

### Phase 2 (In Progress)
- 🔄 User dashboard
- 🔄 Connection wizard
- 🔄 Usage analytics
- 🔄 API key management

### Phase 3 (Planned)
- ⏳ Stripe billing integration
- ⏳ Advanced query features
- ⏳ Team collaboration
- ⏳ Enterprise features

## Environment Variables

Create a `.env.local` file:

```env
# Database
DATABASE_URL="postgresql://user:password@localhost:5432/sqlmcp"

# NextAuth
NEXTAUTH_URL="http://localhost:3000"
NEXTAUTH_SECRET="your-secret-here"

# OAuth providers
GOOGLE_CLIENT_ID=""
GOOGLE_CLIENT_SECRET=""
GITHUB_CLIENT_ID=""
GITHUB_CLIENT_SECRET=""

# Stripe
STRIPE_PUBLIC_KEY=""
STRIPE_SECRET_KEY=""
STRIPE_WEBHOOK_SECRET=""

# Azure (for production)
AZURE_KEY_VAULT_URI=""
AZURE_CLIENT_ID=""
AZURE_CLIENT_SECRET=""
AZURE_TENANT_ID=""
```

## Development

### Running the MCP Server

```bash
# For development
npm run mcp:dev

# For production
npm run mcp:start
```

### Database Commands

```bash
# Push schema changes
npm run db:push

# Open Prisma Studio
npm run db:studio

# Generate Prisma client
npx prisma generate
```

## Architecture

The platform consists of:

1. **Next.js Frontend**: Modern SaaS interface with authentication
2. **MCP Server**: Node.js service implementing the Model Context Protocol
3. **PostgreSQL Database**: User data, connections, and usage tracking
4. **Azure Key Vault**: Secure storage for connection strings

## Security

- All SQL connections encrypted with user-specific keys
- Read-only queries with validation
- Row limits based on subscription tier
- Comprehensive audit logging
- API key authentication for MCP endpoints

## Deployment

### Frontend (Vercel)
```bash
vercel deploy
```

### MCP Server (Azure Container Instances)
```bash
docker build -t sqlmcp-server .
docker push youracr.azurecr.io/sqlmcp-server
az container create --resource-group sqlmcp --name mcp-server --image youracr.azurecr.io/sqlmcp-server
```

## Support

- Documentation: `/docs`
- Email: support@sqlmcp.net
- Discord: [Join our community](#)

## License

Proprietary - All rights reserved