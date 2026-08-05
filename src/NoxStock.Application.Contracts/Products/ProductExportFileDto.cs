namespace NoxStock.Products;

public class ProductExportFileDto
{
    public string FileName { get; set; } = "noxstock-urunler.xlsx";
    public byte[] Content { get; set; } = [];
}
