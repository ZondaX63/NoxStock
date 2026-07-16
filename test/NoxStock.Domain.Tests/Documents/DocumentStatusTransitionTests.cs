using System;
using NoxStock.Documents;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace NoxStock.Domain.Tests.Documents;

public class DocumentStatusTransitionTests
{
    [Fact]
    public void Should_Allow_Commercial_Document_Lifecycle()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid())
        {
            Status = DocumentStatus.Draft
        };

        document.TransitionTo(DocumentStatus.Approved);
        document.TransitionTo(DocumentStatus.PartiallyPaid);
        document.TransitionTo(DocumentStatus.Paid);
        document.TransitionTo(DocumentStatus.Cancelled);

        document.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Should_Reject_Invalid_Or_Backward_Transition()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid())
        {
            Status = DocumentStatus.Paid
        };

        var exception = Should.Throw<BusinessException>(() =>
            document.TransitionTo(DocumentStatus.Approved));

        exception.Code.ShouldBe("NoxStock:InvalidDocumentStatusTransition");
        document.Status.ShouldBe(DocumentStatus.Paid);
    }
}
