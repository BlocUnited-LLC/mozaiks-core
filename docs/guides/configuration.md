# Configuration Guide

Configure MozaiksCore for your environment.

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_URL` | MongoDB connection string | `mongodb://localhost:27017` |
| `SECRET_KEY` | JWT signing key | (required) |
| `DEBUG` | Enable debug mode | `false` |

## Example `.env`

```env
DATABASE_URL=mongodb://localhost:27017/mozaiks-core
SECRET_KEY=your-secret-key-here
DEBUG=false
```
