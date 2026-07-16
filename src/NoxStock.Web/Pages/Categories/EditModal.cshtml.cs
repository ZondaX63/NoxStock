using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NoxStock.Categories;
using Volo.Abp.Application.Dtos;

namespace NoxStock.Web.Pages.Categories;

public class EditModalModel : NoxStockPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateCategoryDto Category { get; set; } = new();

    public List<SelectListItem> Categories { get; set; } = new();

    private readonly ICategoryAppService _categoryAppService;

    public EditModalModel(ICategoryAppService categoryAppService)
    {
        _categoryAppService = categoryAppService;
    }

    public async Task OnGetAsync()
    {
        var categoryDto = await _categoryAppService.GetAsync(Id);
        Category = ObjectMapper.Map<CategoryDto, CreateUpdateCategoryDto>(categoryDto);
        
        var categoryLookup = await _categoryAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 1000,
            Sorting = nameof(CategoryDto.Name)
        });
        
        Categories = categoryLookup.Items
            .Where(c => c.Id != Id) // Prevent self-referencing
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToList();
            
        Categories.Insert(0, new SelectListItem("--- Top Level Category ---", ""));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _categoryAppService.UpdateAsync(Id, Category);
        return NoContent();
    }
}
