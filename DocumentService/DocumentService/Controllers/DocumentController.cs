using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocumentService.Models;
using DocumentService.Services.Interfaces;
using DocumentService.DTOs;

namespace DocumentService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentController(IDocumentService documentService)
        {
            _documentService = documentService;
        }
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest request)
        {
            var result = await _documentService.UploadDocumentAsync(request);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? id, [FromQuery] string? title, [FromQuery] string? type)
        {
            var results = await _documentService.SearchDocumentsAsync(id, title, type);
            return Ok(results);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var doc = await _documentService.GetDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            return Ok(doc);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _documentService.DeleteDocumentAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrReplaceDocument(int id, [FromForm] DocumentUpdateRequest request, IFormFile file = null)
        {
            try
            {
                var result = await _documentService.UpdateOrReplaceDocumentAsync(id, request, file);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

}