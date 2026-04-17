const { PrismaClient } = require('@prisma/client');
const prisma = new PrismaClient();

// GET /posts — with author's name included (JOIN)
app.get('/posts', async (req, res) => {
  const posts = await prisma.post.findMany({
    where: { published: true },
    include: { author: { select: { name: true, email: true } } },
    orderBy: { createdAt: 'desc' },
    take: 20, // pagination limit
    skip: (req.query.page - 1) * 20 || 0
  });
  res.json(posts);
});

// POST /posts — create + validate atomically
app.post('/posts', authenticate, async (req, res) => {
  try {
    const post = await prisma.post.create({
      data: {
        title:    req.body.title,
        content:  req.body.content,
        authorId: req.user.userId  // from JWT
      }
    });
    res.status(201).json(post);
  } catch (e) {
    res.status(400).json({ error: e.message });
  }
});