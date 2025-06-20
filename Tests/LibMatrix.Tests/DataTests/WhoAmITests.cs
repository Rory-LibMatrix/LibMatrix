using LibMatrix.Responses;

namespace LibMatrix.Tests.DataTests;

public static class WhoAmITests {
    public static void VerifyRequiredFields(this WhoAmIResponse obj, bool isAppservice = false) {
        Assert.NotNull(obj);
        Assert.NotNull(obj.UserId);
        if (!isAppservice)
            Assert.NotNull(obj.DeviceId);
    }
}