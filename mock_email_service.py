# mock_email_service.py
from fastapi import FastAPI, HTTPException, Header, Request
import logging

# Configure logging to display in console
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger("mock_email_service")

app = FastAPI()

@app.post("/api/email")
async def send_email(request: Request, authorization: str = Header(None)):
    """Mock endpoint for sending emails"""
    # Check authorization
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Unauthorized")
    
    # Parse the request body
    email_data = await request.json()
    
    # Log what would be sent with clear visual separation
    logger.info("========================== EMAIL SENT ==========================")
    logger.info(f"To: {email_data.get('recipient')}")
    logger.info(f"Subject: {email_data.get('subject')}")
    logger.info(f"Message: {email_data.get('message')}")
    logger.info(f"Type: {email_data.get('notification_type')}")
    logger.info("================================================================")
    
    # Return success response
    return {"status": "sent", "message": "Email would be sent in production"}

if __name__ == "__main__":
    import uvicorn
    # Make sure to print a startup message so it's clear the service is running
    print("\n🚀 Mock Email Service is running at http://localhost:5000")
    print("📧 Emails will be logged to this console\n")
    uvicorn.run("mock_email_service:app", host="0.0.0.0", port=5000, reload=True)