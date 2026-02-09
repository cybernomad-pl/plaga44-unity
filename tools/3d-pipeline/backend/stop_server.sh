#!/bin/bash

echo "========================================"
echo "  Stopping cbrnmd.3D Backend Server"
echo "========================================"

# Kill processes on port 5000
if lsof -ti:5000 > /dev/null 2>&1; then
    echo "Killing Flask server on port 5000..."
    kill -9 $(lsof -ti:5000) 2>/dev/null || true
    echo "✓ Server stopped"
else
    echo "No server running on port 5000"
fi

echo "========================================"
