using AutoMapper;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Customers;
using SalesDesk.Application.Documents;
using SalesDesk.Application.Products;
using SalesDesk.Application.Templates;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Common.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(dest => dest.LifetimeValue, opt => opt.Ignore());
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
        CreateMap<Product, ProductDto>();
        CreateMap<Template, TemplateDto>();

        CreateMap<DocumentLineItem, DocumentLineItemDto>();

        // Customer/Template are required navigations on every persisted Document,
        // so handlers always load them (.Include) before mapping — see
        // GetDocumentsQuery/GetDocumentByIdQuery.
        CreateMap<Document, DocumentDto>()
            .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer!.Name))
            .ForMember(dest => dest.CustomerCompany, opt => opt.MapFrom(src => src.Customer!.Company))
            .ForMember(dest => dest.TemplateName, opt => opt.MapFrom(src => src.Template!.Name));
    }
}
