using KuaiYi.Core.Installation;
using System.Text;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstallationImageOfferTests
{
    private static InstallationImageOffer Offer()=>new(Guid.NewGuid(),new string('A',64),Guid.NewGuid(),"C:\\public\\合成 image.gif");
    [Fact]public void CanonicalOfferRoundTripsUnicodeAndBindsTheExactPathAndRequest()
    {var offer=Offer();Assert.Equal(offer,InstallationImageOffer.Decode(offer.Encode()));Assert.NotEqual(offer.Fingerprint,(offer with{Path="C:\\public\\another.png"}).Fingerprint);Assert.NotEqual(offer.Fingerprint,(offer with{Request=Guid.NewGuid()}).Fingerprint);}
    [Theory][InlineData("before")][InlineData("after")][InlineData("middle")][InlineData("newline")]
    public void Base64WhitespaceAliasesAreRejectedEvenWhenDecodedJsonIsIdentical(string alias)
    {
        string encoded=Offer().Encode();string candidate=alias switch{"before"=>" "+encoded,"after"=>encoded+" ","middle"=>encoded.Insert(4," "),_=>encoded.Insert(4,"\n")};
        Assert.Equal(Convert.FromBase64String(encoded),Convert.FromBase64String(candidate));Assert.Throws<InvalidDataException>(()=>InstallationImageOffer.Decode(candidate));
    }
    [Fact]public void OversizedAndNoncanonicalJsonCannotBecomeAnOffer()
    {
        Assert.Throws<InvalidDataException>(()=>InstallationImageOffer.Decode(new string('A',49153)));
        string json=Encoding.UTF8.GetString(Convert.FromBase64String(Offer().Encode()));
        Assert.Throws<InvalidDataException>(()=>InstallationImageOffer.Decode(Convert.ToBase64String(Encoding.UTF8.GetBytes(" "+json))));
    }
}
