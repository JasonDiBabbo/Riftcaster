using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LowerThirdKeywordMessage), "keyword")]
[JsonDerivedType(typeof(LowerThirdInformationMessage), "information")]
[JsonDerivedType(typeof(LowerThirdSocialsMessage), "socials")]
public abstract record LowerThirdMessage;
