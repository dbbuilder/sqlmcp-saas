import { NextApiRequest, NextApiResponse } from 'next'
import { getServerSession } from 'next-auth'
import { PrismaClient } from '@prisma/client'
import crypto from 'crypto'
import { authOptions } from '../auth/[...nextauth]'

const prisma = new PrismaClient()

// Simple encryption for connection passwords
// In production, use Azure Key Vault or similar
function encryptPassword(password: string, userId: string): string {
  const algorithm = 'aes-256-cbc'
  const key = crypto.scryptSync(process.env.ENCRYPTION_KEY || 'default-key', userId, 32)
  const iv = crypto.randomBytes(16)
  const cipher = crypto.createCipheriv(algorithm, key, iv)
  
  let encrypted = cipher.update(password, 'utf8', 'hex')
  encrypted += cipher.final('hex')
  
  return iv.toString('hex') + ':' + encrypted
}

function decryptPassword(encryptedData: string, userId: string): string {
  const algorithm = 'aes-256-cbc'
  const key = crypto.scryptSync(process.env.ENCRYPTION_KEY || 'default-key', userId, 32)
  const [ivHex, encrypted] = encryptedData.split(':')
  const iv = Buffer.from(ivHex, 'hex')
  
  const decipher = crypto.createDecipheriv(algorithm, key, iv)
  let decrypted = decipher.update(encrypted, 'hex', 'utf8')
  decrypted += decipher.final('utf8')
  
  return decrypted
}

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse
) {
  const session = await getServerSession(req, res, authOptions)
  if (!session || !session.user?.id) {
    return res.status(401).json({ error: 'Unauthorized' })
  }

  const userId = session.user.id

  switch (req.method) {
    case 'GET':
      // Get all connections for the user
      try {
        const connections = await prisma.connection.findMany({
          where: {
            userId,
          },
          select: {
            id: true,
            name: true,
            server: true,
            database: true,
            port: true,
            isActive: true,
            lastTestedAt: true,
            createdAt: true,
          },
          orderBy: {
            createdAt: 'desc',
          },
        })

        return res.status(200).json(connections)
      } catch (error) {
        console.error('Error fetching connections:', error)
        return res.status(500).json({ error: 'Failed to fetch connections' })
      }

    case 'POST':
      // Create a new connection
      try {
        const {
          name,
          server,
          database,
          port = 1433,
          username,
          password,
          trustServerCertificate = false,
        } = req.body

        // Validate required fields
        if (!name || !server || !database || !username || !password) {
          return res.status(400).json({ error: 'Missing required fields' })
        }

        // Check if connection name already exists for this user
        const existing = await prisma.connection.findUnique({
          where: {
            userId_name: {
              userId,
              name,
            },
          },
        })

        if (existing) {
          return res.status(409).json({ error: 'Connection name already exists' })
        }

        // Check user's connection limit based on subscription
        const user = await prisma.user.findUnique({
          where: { id: userId },
          include: { subscription: true },
        })

        const connectionCount = await prisma.connection.count({
          where: { userId },
        })

        const limits = {
          FREE: 1,
          STARTER: 3,
          PRO: 10,
          ENTERPRISE: 999,
        }

        const plan = user?.subscription?.plan || 'FREE'
        const limit = limits[plan]

        if (connectionCount >= limit) {
          return res.status(403).json({
            error: `Connection limit reached. Your ${plan} plan allows ${limit} connection${limit > 1 ? 's' : ''}.`,
          })
        }

        // Encrypt the password
        const encryptedPassword = encryptPassword(password, userId)

        // Create the connection
        const connection = await prisma.connection.create({
          data: {
            userId,
            name,
            server,
            database,
            port,
            username,
            encryptedPassword,
            trustServerCert: trustServerCertificate,
            isActive: true,
            lastTestedAt: new Date(), // Assume it was tested during creation
          },
          select: {
            id: true,
            name: true,
            server: true,
            database: true,
            createdAt: true,
          },
        })

        return res.status(201).json(connection)
      } catch (error) {
        console.error('Error creating connection:', error)
        return res.status(500).json({ error: 'Failed to create connection' })
      }

    default:
      return res.status(405).json({ error: 'Method not allowed' })
  }
}