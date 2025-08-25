# Protocol Tests

This directory contains unit tests for the IPC Protocol layer.

## Test Structure

### Client Tests
- **SuggestionServiceClientTests.cs** - Tests for client behavior and error handling

### Server Tests
- **SuggestionServiceServerTests.cs** - Tests for server creation and disposal

### Mocks
- **MockSuggestionService.cs** - Simple mock implementation for testing

## Running Tests

### Build and Run All Tests
```bash
dotnet test tests/Protocol.Tests/
```

### Run Specific Test Categories
```bash
# Run only client tests
dotnet test tests/Protocol.Tests/ --filter "FullyQualifiedName~Client"

# Run only server tests
dotnet test tests/Protocol.Tests/ --filter "FullyQualifiedName~Server"
```

### Run with Coverage
```bash
dotnet test tests/Protocol.Tests/ --collect:"XPlat Code Coverage"
```

## Test Philosophy

These tests focus on:

1. **Unit Testing**: Each component is tested in isolation
2. **Error Handling**: Verify degradation and error responses
3. **Resource Management**: Ensure proper disposal and cleanup
4. **Simplified Scope**: Focus on essential functionality for hackathon

## Test Coverage

- ✅ **Client**: Basic functionality and error handling
- ✅ **Server**: Creation, disposal, and basic lifecycle
- ✅ **Mocks**: Simple mock service for testing

## Notes

- Tests focus on the 20ms response time constraint for client operations
- Verify proper resource cleanup to prevent memory leaks
- Test error scenarios to ensure robust error handling
- Models and factory are simple enough to not require dedicated tests

## Why No "Successful" Test Cases?

Our current tests focus on **error handling and edge cases** rather than "successful" scenarios because:

### **Current Test Philosophy:**
- **Error-First Testing**: Verify degradation when things go wrong
- **Resource Management**: Ensure proper cleanup and disposal
- **Edge Cases**: Test cancellation, service unavailability, null parameters

### **Why No Real IPC Communication Tests:**
- **No Real Server**: We don't have a running service to connect to
- **No Real LLM**: The actual suggestion service isn't implemented yet
- **Focus on Protocol Layer**: Testing the communication infrastructure, not business logic

### **What We're Actually Testing:**
✅ **Client Error Handling**: Returns empty responses when service unavailable  
✅ **Server Lifecycle**: Proper creation and disposal  
✅ **Resource Cleanup**: No memory leaks or resource exhaustion  
✅ **Parameter Validation**: Proper null checks and validation  

### **Future Integration Tests:**
When the actual service is implemented, we'll add:
- Real client-server communication tests
- Performance tests (20ms constraint verification)
- End-to-end suggestion generation tests
- Load testing with multiple clients

**For now, these tests ensure our protocol layer is robust and won't crash the PowerShell host process!**
