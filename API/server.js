const express = require('express');
const app = express();

app.use(express.json()); // Parse JSON request bodies

// In-memory "database" for this example
let posts = [
  { id: 1, title: 'Hello World', author: 'Alice' },
  { id: 2, title: 'REST APIs',  author: 'Bob'   },
];
let nextId = 3;

// GET /posts — list all, with optional ?author= filter
app.get('/posts', (req, res) => {
  const { author } = req.query; // query params
  let result = posts;
  if (author) {
    result = posts.filter(p => p.author === author);
  }
  res.status(200).json({ data: result, total: result.length });
});

// GET /posts/:id — get one post by path param
app.get('/posts/:id', (req, res) => {
  const post = posts.find(p => p.id === parseInt(req.params.id));
  if (!post) return res.status(404).json({ error: 'Post not found' });
  res.json(post);
});

// POST /posts — create a new post
app.post('/posts', (req, res) => {
  const { title, author } = req.body;
  if (!title || !author) {
    return res.status(400).json({ error: 'title and author required' });
  }
  const post = { id: nextId++, title, author };
  posts.push(post);
  res.status(201).json(post); // 201 Created
});

// PATCH /posts/:id — partial update
app.patch('/posts/:id', (req, res) => {
  const idx = posts.findIndex(p => p.id === parseInt(req.params.id));
  if (idx === -1) return res.status(404).json({ error: 'Not found' });
  posts[idx] = { ...posts[idx], ...req.body }; // merge changes
  res.json(posts[idx]);
});

// DELETE /posts/:id
app.delete('/posts/:id', (req, res) => {
  const idx = posts.findIndex(p => p.id === parseInt(req.params.id));
  if (idx === -1) return res.status(404).json({ error: 'Not found' });
  posts.splice(idx, 1);
  res.status(204).send(); // 204 No Content
});

app.listen(3000, () => console.log('API running on :3000'));