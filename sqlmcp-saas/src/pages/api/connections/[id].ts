import { NextApiRequest, NextApiResponse } from 'next'
import { getServerSession } from 'next-auth'
import { PrismaClient } from '@prisma/client'
import { authOptions } from '../auth/[...nextauth]'

const prisma = new PrismaClient()

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse
) {
  const session = await getServerSession(req, res, authOptions)
  if (!session || !session.user?.id) {
    return res.status(401).json({ error: 'Unauthorized' })
  }

  const userId = session.user.id
  const connectionId = req.query.id as string

  // Verify the connection belongs to the user
  const connection = await prisma.connection.findFirst({
    where: {
      id: connectionId,
      userId,
    },
  })

  if (!connection) {
    return res.status(404).json({ error: 'Connection not found' })
  }

  switch (req.method) {
    case 'GET':
      // Get connection details (without password)
      return res.status(200).json({
        id: connection.id,
        name: connection.name,
        server: connection.server,
        database: connection.database,
        port: connection.port,
        username: connection.username,
        trustServerCert: connection.trustServerCert,
        isActive: connection.isActive,
        lastTestedAt: connection.lastTestedAt,
        createdAt: connection.createdAt,
        updatedAt: connection.updatedAt,
      })

    case 'PATCH':
      // Update connection
      try {
        const updates = req.body
        
        // Don't allow updating certain fields
        delete updates.id
        delete updates.userId
        delete updates.encryptedPassword // Handle password updates separately
        
        // If updating name, check for duplicates
        if (updates.name && updates.name !== connection.name) {
          const existing = await prisma.connection.findFirst({
            where: {
              userId,
              name: updates.name,
              NOT: { id: connectionId },
            },
          })
          
          if (existing) {
            return res.status(409).json({ error: 'Connection name already exists' })
          }
        }

        const updated = await prisma.connection.update({
          where: { id: connectionId },
          data: {
            ...updates,
            updatedAt: new Date(),
          },
          select: {
            id: true,
            name: true,
            server: true,
            database: true,
            updatedAt: true,
          },
        })

        return res.status(200).json(updated)
      } catch (error) {
        console.error('Error updating connection:', error)
        return res.status(500).json({ error: 'Failed to update connection' })
      }

    case 'DELETE':
      // Delete connection
      try {
        // Delete all associated queries first
        await prisma.query.deleteMany({
          where: { connectionId },
        })

        // Delete the connection
        await prisma.connection.delete({
          where: { id: connectionId },
        })

        return res.status(200).json({ success: true })
      } catch (error) {
        console.error('Error deleting connection:', error)
        return res.status(500).json({ error: 'Failed to delete connection' })
      }

    default:
      return res.status(405).json({ error: 'Method not allowed' })
  }
}