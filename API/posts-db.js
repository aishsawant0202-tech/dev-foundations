const express = require('express')
const { PrismaClient } = require('@prisma/client')

const app = express()
const prisma = new PrismaClient()

app.use(express.json())

// GET all posts
app.get('/posts', async (req, res) => {
  const posts = await prisma.post.findMany({
    include: { author: true }
  })
  res.json(posts)
})

// POST create a new post
app.post('/posts', async (req, res) => {
  try {
    const post = await prisma.post.create({
      data: {
        title:    req.body.title,
        content:  req.body.content,
        authorId: req.body.authorId
      }
    })
    res.status(201).json(post)
  } catch (e) {
    res.status(400).json({ error: e.message })
  }
})

app.listen(3000, () => console.log('API running on :3000'))