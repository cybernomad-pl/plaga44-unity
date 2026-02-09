#!/bin/bash

echo "========================================"
echo "  cbrnmd.3D Tutorial Backend Server"
echo "========================================"
echo ""

# Check if virtual environment exists
if [ ! -d "venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
source venv/bin/activate

# Install dependencies
echo "Installing dependencies..."
pip install -q -r backend/requirements.txt

# Check if COLMAP is installed
if ! command -v colmap &> /dev/null; then
    echo "ERROR: COLMAP not found!"
    echo "Please install COLMAP first:"
    echo "  sudo apt install colmap"
    exit 1
fi

echo ""
echo "Checking for running instances..."

# Kill any existing Flask server on port 5000
if lsof -ti:5000 > /dev/null 2>&1; then
    echo "Found existing server on port 5000, killing it..."
    kill -9 $(lsof -ti:5000) 2>/dev/null || true
    sleep 1
fi

echo ""
echo "Starting Flask API server..."
echo "Server will be available at: http://localhost:5000"
echo "Open tutorial.html in your browser to use the interactive tutorial"
echo ""
echo "Press Ctrl+C to stop the server"
echo "========================================"
echo ""

# Run the API server
python3 backend/api.py
