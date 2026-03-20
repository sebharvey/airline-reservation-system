// Author: Seb Harvey
// Description: Serves the OpenAPI 3.0 specification for the Template API at GET /swagger.json.
//              Update this document whenever endpoints or schemas change.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace ReservationSystem.Template.TemplateApi.Functions;

/// <summary>
/// Exposes the OpenAPI 3.0 document for this API at GET /swagger.json.
/// No additional NuGet packages are required — the document is maintained
/// alongside the code and returned as a plain JSON response.
/// </summary>
public sealed class SwaggerFunction
{
    [Function("SwaggerJson")]
    public async Task<HttpResponseData> GetSwaggerJson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(OpenApiDocument, cancellationToken);
        return response;
    }

    // -------------------------------------------------------------------------
    // OpenAPI 3.0 document
    // Keep in sync with the HTTP-triggered functions in this project.
    // -------------------------------------------------------------------------

    private const string OpenApiDocument = """
        {
          "openapi": "3.0.3",
          "info": {
            "title": "Apex Air \u2013 Template API",
            "description": "Reference scaffold for all Apex Air microservices. Demonstrates Clean Architecture conventions, CRUD patterns for Person and TemplateItem resources, and external service integration (exchange-rate client).",
            "version": "1.0.0"
          },
          "servers": [
            {
              "url": "http://localhost:7071",
              "description": "Local development"
            }
          ],
          "tags": [
            { "name": "Health",        "description": "Liveness and readiness probes" },
            { "name": "Persons",       "description": "CRUD operations for the Person resource" },
            { "name": "TemplateItems", "description": "CRUD operations for the TemplateItem resource" }
          ],
          "paths": {
            "/v1/hello": {
              "get": {
                "tags": ["Health"],
                "operationId": "HelloWorld",
                "summary": "Smoke-test endpoint",
                "description": "Returns a Hello World message to verify the function host is running.",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "message": { "type": "string", "example": "Hello, World!" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            },
            "/v1/health": {
              "get": {
                "tags": ["Health"],
                "operationId": "HealthCheck",
                "summary": "Health check",
                "description": "Returns 200 when all registered health-check providers succeed, 503 otherwise.",
                "responses": {
                  "200": { "description": "Healthy" },
                  "503": { "description": "Unhealthy" }
                }
              }
            },
            "/v1/persons": {
              "get": {
                "tags": ["Persons"],
                "operationId": "GetAllPersons",
                "summary": "List all persons",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "array",
                          "items": { "$ref": "#/components/schemas/PersonResponse" }
                        }
                      }
                    }
                  }
                }
              },
              "post": {
                "tags": ["Persons"],
                "operationId": "CreatePerson",
                "summary": "Create a person",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/CreatePersonRequest" }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "headers": {
                      "Location": {
                        "description": "URL of the created resource",
                        "schema": { "type": "string" }
                      }
                    },
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/PersonResponse" }
                      }
                    }
                  },
                  "400": { "description": "Bad Request \u2013 missing or invalid fields" },
                  "409": { "description": "Conflict \u2013 a person with that personId already exists" }
                }
              }
            },
            "/v1/persons/{id}": {
              "parameters": [
                {
                  "name": "id",
                  "in": "path",
                  "required": true,
                  "description": "Numeric PersonID",
                  "schema": { "type": "integer", "format": "int32" }
                }
              ],
              "get": {
                "tags": ["Persons"],
                "operationId": "GetPerson",
                "summary": "Get a person by ID",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/PersonResponse" }
                      }
                    }
                  },
                  "404": { "description": "Not Found" }
                }
              },
              "put": {
                "tags": ["Persons"],
                "operationId": "UpdatePerson",
                "summary": "Update a person",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/UpdatePersonRequest" }
                    }
                  }
                },
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/PersonResponse" }
                      }
                    }
                  },
                  "400": { "description": "Bad Request" },
                  "404": { "description": "Not Found" }
                }
              },
              "delete": {
                "tags": ["Persons"],
                "operationId": "DeletePerson",
                "summary": "Delete a person",
                "responses": {
                  "204": { "description": "Deleted" },
                  "404": { "description": "Not Found" }
                }
              }
            },
            "/v1/template-items": {
              "get": {
                "tags": ["TemplateItems"],
                "operationId": "GetAllTemplateItems",
                "summary": "List all template items",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "array",
                          "items": { "$ref": "#/components/schemas/TemplateItemResponse" }
                        }
                      }
                    }
                  }
                }
              },
              "post": {
                "tags": ["TemplateItems"],
                "operationId": "CreateTemplateItem",
                "summary": "Create a template item",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/CreateTemplateItemRequest" }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "headers": {
                      "Location": {
                        "description": "URL of the created resource",
                        "schema": { "type": "string" }
                      }
                    },
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/TemplateItemResponse" }
                      }
                    }
                  },
                  "400": { "description": "Bad Request \u2013 missing or invalid fields" }
                }
              }
            },
            "/v1/template-items/{id}": {
              "parameters": [
                {
                  "name": "id",
                  "in": "path",
                  "required": true,
                  "description": "Template item UUID",
                  "schema": { "type": "string", "format": "uuid" }
                }
              ],
              "get": {
                "tags": ["TemplateItems"],
                "operationId": "GetTemplateItem",
                "summary": "Get a template item by ID",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/TemplateItemResponse" }
                      }
                    }
                  },
                  "404": { "description": "Not Found" }
                }
              },
              "delete": {
                "tags": ["TemplateItems"],
                "operationId": "DeleteTemplateItem",
                "summary": "Delete a template item",
                "responses": {
                  "204": { "description": "Deleted" },
                  "404": { "description": "Not Found" }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "PersonResponse": {
                "type": "object",
                "required": ["personId", "lastName"],
                "properties": {
                  "personId":  { "type": "integer", "format": "int32", "example": 1 },
                  "lastName":  { "type": "string", "example": "Smith" },
                  "firstName": { "type": "string", "nullable": true, "example": "John" },
                  "address":   { "type": "string", "nullable": true, "example": "123 Main Street" },
                  "city":      { "type": "string", "nullable": true, "example": "London" }
                }
              },
              "CreatePersonRequest": {
                "type": "object",
                "required": ["personId", "lastName"],
                "properties": {
                  "personId":  { "type": "integer", "format": "int32", "example": 1 },
                  "lastName":  { "type": "string", "example": "Smith" },
                  "firstName": { "type": "string", "nullable": true, "example": "John" },
                  "address":   { "type": "string", "nullable": true, "example": "123 Main Street" },
                  "city":      { "type": "string", "nullable": true, "example": "London" }
                }
              },
              "UpdatePersonRequest": {
                "type": "object",
                "required": ["lastName"],
                "properties": {
                  "lastName":  { "type": "string", "example": "Smith" },
                  "firstName": { "type": "string", "nullable": true, "example": "John" },
                  "address":   { "type": "string", "nullable": true, "example": "123 Main Street" },
                  "city":      { "type": "string", "nullable": true, "example": "London" }
                }
              },
              "TemplateItemResponse": {
                "type": "object",
                "required": ["id", "name", "status", "metadata", "createdAt", "updatedAt"],
                "properties": {
                  "id":        { "type": "string", "format": "uuid", "example": "3fa85f64-5717-4562-b3fc-2c963f66afa6" },
                  "name":      { "type": "string", "example": "Sample item" },
                  "status":    { "type": "string", "example": "active" },
                  "metadata":  { "$ref": "#/components/schemas/TemplateItemMetadataResponse" },
                  "createdAt": { "type": "string", "format": "date-time", "example": "2024-01-15T10:00:00Z" },
                  "updatedAt": { "type": "string", "format": "date-time", "example": "2024-01-15T10:00:00Z" }
                }
              },
              "TemplateItemMetadataResponse": {
                "type": "object",
                "required": ["tags", "priority", "properties"],
                "properties": {
                  "tags": {
                    "type": "array",
                    "items": { "type": "string" },
                    "example": ["urgent", "review"]
                  },
                  "priority": { "type": "string", "example": "high" },
                  "properties": {
                    "type": "object",
                    "additionalProperties": { "type": "string" },
                    "example": { "color": "blue", "size": "large" }
                  }
                }
              },
              "CreateTemplateItemRequest": {
                "type": "object",
                "required": ["name"],
                "properties": {
                  "name": { "type": "string", "example": "Sample item" },
                  "tags": {
                    "type": "array",
                    "items": { "type": "string" },
                    "example": ["urgent", "review"]
                  },
                  "priority": { "type": "string", "example": "normal", "default": "normal" },
                  "properties": {
                    "type": "object",
                    "additionalProperties": { "type": "string" },
                    "example": { "color": "blue" }
                  }
                }
              }
            }
          }
        }
        """;
}
