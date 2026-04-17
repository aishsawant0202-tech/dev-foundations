const express = require('express');
const jwt     = require('jsonwebtoken');
const bcrypt  = require('bcrypt');
const app     = express();
app.use(express.json());

const JWT_SECRET = 'never-commit-secrets-use-env-vars!';

// Simulated user store (use a real DB in production)
const users = [];

// --- REGISTER ---
app.post('/auth/register', async (req, res) => {
  const { email, password } = req.body;
  if (users.find(u => u.email === email))
    return res.status(409).json({ error: 'Email already exists' });

  const hash = await bcrypt.hash(password, 12); // salt rounds = 12
  const user = { id: users.length + 1, email, hash, role: 'user' };
  users.push(user);
  res.status(201).json({ message: 'Registered', userId: user.id });
});

// --- LOGIN — issues a JWT ---
app.post('/auth/login', async (req, res) => {
  const { email, password } = req.body;
  const user = users.find(u => u.email === email);
  if (!user || !(await bcrypt.compare(password, user.hash)))
    return res.status(401).json({ error: 'Invalid credentials' });

  const token = jwt.sign(
    { userId: user.id, role: user.role }, // payload (claims)
    JWT_SECRET,
    { expiresIn: '5m' } // short-lived access token
  );
  res.json({ token });
});

// --- MIDDLEWARE: Verify JWT on protected routes ---
function authenticate(req, res, next) {
  const authHeader = req.headers['authorization'];
  if (!authHeader?.startsWith('Bearer '))
    return res.status(401).json({ error: 'No token' });

  try {
    req.user = jwt.verify(authHeader.split(' ')[1], JWT_SECRET);
    next(); // token valid, continue to route handler
  } catch {
    res.status(403).json({ error: 'Invalid or expired token' });
  }
}

// --- MIDDLEWARE: Check role for authorization ---
function requireAdmin(req, res, next) {
  if (req.user.role !== 'admin')
    return res.status(403).json({ error: 'Admin only' });
  next();
}

// Protected route — any logged in user
app.get('/profile', authenticate, (req, res) => {
  res.json({ message: 'Your profile', user: req.user });
});

// Protected route — admins only
app.get('/admin', authenticate, requireAdmin, (req, res) => {
  res.json({ message: 'Admin panel' });
});

app.listen(3000, () => console.log('Auth server running on :3000'));