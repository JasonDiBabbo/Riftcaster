using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LowerThirdKeywordMessage), "keyword")]
public abstract record LowerThirdMessage;
