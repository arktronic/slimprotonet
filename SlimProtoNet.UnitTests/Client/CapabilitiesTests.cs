using SlimProtoNet.Client;

namespace SlimProtoNet.UnitTests.Client;

[TestClass]
public class CapabilitiesTests
{
    [TestMethod]
    public void DefaultCapabilitiesShouldIncludeSqueezeliteDefaults()
    {
        var capabilities = new Capabilities();
        var result = capabilities.ToString();

        Assert.AreEqual("Model=squeezelite,ModelName=SqueezeLite,AccuratePlayPoints=1,HasDigitalOut=1,HasPreAmp=1,HasDisableDac=1", result);
    }

    [TestMethod]
    public void AddShouldAppendSingleCapability()
    {
        var capabilities = new Capabilities();
        capabilities.Add(new CapabilityValue(Capability.Mp3));
        var result = capabilities.ToString();

        Assert.AreEqual("Model=squeezelite,ModelName=SqueezeLite,AccuratePlayPoints=1,HasDigitalOut=1,HasPreAmp=1,HasDisableDac=1,mp3", result);
    }

    [TestMethod]
    public void AddShouldAppendListWithValues()
    {
        var capabilities = new Capabilities();
        capabilities.Add(new CapabilityValue(Capability.Mp3));
        capabilities.Add(new CapabilityValue(Capability.MaxSampleRate, "9600"));
        capabilities.Add(new CapabilityValue(Capability.Ogg));
        var result = capabilities.ToString();

        Assert.AreEqual("Model=squeezelite,ModelName=SqueezeLite,AccuratePlayPoints=1,HasDigitalOut=1,HasPreAmp=1,HasDisableDac=1,mp3,MaxSampleRate=9600,ogg", result);
    }

    [TestMethod]
    public void AddNameShouldReplaceModelName()
    {
        var capabilities = new Capabilities();
        capabilities.AddName("Testing");
        var result = capabilities.ToString();

        Assert.AreEqual("Model=squeezelite,AccuratePlayPoints=1,HasDigitalOut=1,HasPreAmp=1,HasDisableDac=1,ModelName=Testing", result);
    }

    [TestMethod]
    public void AddShouldReplaceExistingCapabilityOfSameType()
    {
        var capabilities = new Capabilities();
        capabilities.Add(new CapabilityValue(Capability.Model, "custom1"));
        capabilities.Add(new CapabilityValue(Capability.Model, "custom2"));
        var result = capabilities.ToString();

        Assert.Contains("Model=custom2", result);
        Assert.DoesNotContain("Model=custom1", result);
        Assert.DoesNotContain("Model=squeezelite", result);
    }

    [TestMethod]
    public void CapabilityValueToStringShouldFormatCorrectly()
    {
        Assert.AreEqual("mp3", new CapabilityValue(Capability.Mp3).ToString());
        Assert.AreEqual("flc", new CapabilityValue(Capability.Flc).ToString());
        Assert.AreEqual("pcm", new CapabilityValue(Capability.Pcm).ToString());
        Assert.AreEqual("aac", new CapabilityValue(Capability.Aac).ToString());
        Assert.AreEqual("MaxSampleRate=96000", new CapabilityValue(Capability.MaxSampleRate, "96000").ToString());
        Assert.AreEqual("Model=TestModel", new CapabilityValue(Capability.Model, "TestModel").ToString());
        Assert.AreEqual("ModelName=TestName", new CapabilityValue(Capability.ModelName, "TestName").ToString());
        Assert.AreEqual("AccuratePlayPoints=1", new CapabilityValue(Capability.AccuratePlayPoints).ToString());
        Assert.AreEqual("HasDigitalOut=1", new CapabilityValue(Capability.HasDigitalOut).ToString());
        Assert.AreEqual("HasPreAmp=1", new CapabilityValue(Capability.HasPreAmp).ToString());
        Assert.AreEqual("HasDisableDac=1", new CapabilityValue(Capability.HasDisableDAC).ToString());
        Assert.AreEqual("Balance=1", new CapabilityValue(Capability.Balance).ToString());
        Assert.AreEqual("CanHTTPS=1", new CapabilityValue(Capability.CanHTTPS).ToString());
    }

    [TestMethod]
    public void CapabilityValueEqualityShouldCompareBothTypeAndValue()
    {
        var cap1 = new CapabilityValue(Capability.Model, "test");
        var cap2 = new CapabilityValue(Capability.Model, "test");
        var cap3 = new CapabilityValue(Capability.Model, "different");
        var cap4 = new CapabilityValue(Capability.ModelName, "test");

        // Same type and value should be equal
        Assert.IsTrue(cap1.Equals(cap2));
        Assert.IsTrue(cap2.Equals(cap1));
        Assert.AreEqual(cap1, cap2);

        // Same type, different value should NOT be equal
        Assert.IsFalse(cap1.Equals(cap3));
        Assert.AreNotEqual(cap1, cap3);

        // Different type, same value should NOT be equal
        Assert.IsFalse(cap1.Equals(cap4));
        Assert.AreNotEqual(cap1, cap4);
    }

    [TestMethod]
    public void CapabilityValueEqualityShouldHandleNullValues()
    {
        var cap1 = new CapabilityValue(Capability.Mp3);
        var cap2 = new CapabilityValue(Capability.Mp3);
        var cap3 = new CapabilityValue(Capability.Mp3, "value");

        // Both null values should be equal
        Assert.AreEqual(cap1, cap2);

        // Null vs non-null should NOT be equal
        Assert.AreNotEqual(cap1, cap3);
    }

    [TestMethod]
    public void CapabilityValueGetHashCodeShouldBeConsistent()
    {
        var cap1 = new CapabilityValue(Capability.Model, "test");
        var cap2 = new CapabilityValue(Capability.Model, "test");
        var cap3 = new CapabilityValue(Capability.Model, "different");

        // Equal objects must have same hash code
        Assert.AreEqual(cap1.GetHashCode(), cap2.GetHashCode());

        // Different objects should (ideally) have different hash codes
        Assert.AreNotEqual(cap1.GetHashCode(), cap3.GetHashCode());
    }

    [TestMethod]
    public void AddShouldThrowWhenCapabilityIsNull()
    {
        var capabilities = new Capabilities();

        var ex = Assert.Throws<ArgumentNullException>(() => capabilities.Add(null!));
        Assert.AreEqual("capability", ex.ParamName);
    }

    [TestMethod]
    public void AddNameShouldThrowWhenNameIsNullOrEmpty()
    {
        var capabilities = new Capabilities();

        var ex1 = Assert.Throws<ArgumentException>(() => capabilities.AddName(null!));
        Assert.AreEqual("name", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => capabilities.AddName(""));
        Assert.AreEqual("name", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => capabilities.AddName("   "));
        Assert.AreEqual("name", ex3.ParamName);
    }

    [TestMethod]
    public void CustomCapabilityShouldOutputRawString()
    {
        var cap1 = new CapabilityValue("opus");
        var cap2 = new CapabilityValue("MyFeature=1");
        var cap3 = new CapabilityValue("CustomCodec=enabled");

        Assert.AreEqual("opus", cap1.ToString());
        Assert.AreEqual("MyFeature=1", cap2.ToString());
        Assert.AreEqual("CustomCodec=enabled", cap3.ToString());
    }

    [TestMethod]
    public void CustomCapabilityShouldBeMarkedAsCustom()
    {
        var customCap = new CapabilityValue("opus");
        var predefinedCap = new CapabilityValue(Capability.Mp3);

        Assert.IsTrue(customCap.IsCustom);
        Assert.IsFalse(predefinedCap.IsCustom);
    }

    [TestMethod]
    public void AddShouldSupportCustomCapabilities()
    {
        var capabilities = new Capabilities(false);
        capabilities.Add(new CapabilityValue(Capability.Pcm));
        capabilities.Add(new CapabilityValue("opus"));
        capabilities.Add(new CapabilityValue(Capability.Mp3));
        capabilities.Add(new CapabilityValue("MyFeature=1"));

        var result = capabilities.ToString();
        Assert.AreEqual("pcm,opus,mp3,MyFeature=1", result);
    }

    [TestMethod]
    public void CustomCapabilityConstructorShouldThrowWhenStringIsNullOrEmpty()
    {
        var ex1 = Assert.Throws<ArgumentException>(() => new CapabilityValue((string)null!));
        Assert.AreEqual("customName", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => new CapabilityValue(""));
        Assert.AreEqual("customName", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => new CapabilityValue("   "));
        Assert.AreEqual("customName", ex3.ParamName);
    }

    [TestMethod]
    public void CustomCapabilityEqualityShouldCompareRawStrings()
    {
        var cap1 = new CapabilityValue("opus");
        var cap2 = new CapabilityValue("opus");
        var cap3 = new CapabilityValue("flac");
        var cap4 = new CapabilityValue(Capability.Flc);

        // Same custom string should be equal
        Assert.IsTrue(cap1.Equals(cap2));
        Assert.AreEqual(cap1, cap2);

        // Different custom strings should NOT be equal
        Assert.IsFalse(cap1.Equals(cap3));
        Assert.AreNotEqual(cap1, cap3);

        // Custom vs predefined should NOT be equal (even if they serialize the same)
        Assert.IsFalse(cap3.Equals(cap4));
        Assert.AreNotEqual(cap3, cap4);
    }

    [TestMethod]
    public void CustomCapabilityGetHashCodeShouldBeConsistent()
    {
        var cap1 = new CapabilityValue("opus");
        var cap2 = new CapabilityValue("opus");
        var cap3 = new CapabilityValue("flac");

        // Equal objects must have same hash code
        Assert.AreEqual(cap1.GetHashCode(), cap2.GetHashCode());

        // Different objects should (ideally) have different hash codes
        Assert.AreNotEqual(cap1.GetHashCode(), cap3.GetHashCode());
    }

    [TestMethod]
    public void AddShouldNotDeduplicateCustomCapabilities()
    {
        // Custom capabilities with the same string can be added multiple times
        // (user responsibility to manage)
        var capabilities = new Capabilities(false);
        capabilities.Add(new CapabilityValue("custom1"));
        capabilities.Add(new CapabilityValue("custom1"));

        var result = capabilities.ToString();
        Assert.AreEqual("custom1,custom1", result);
    }

    [TestMethod]
    public void AddShouldDeduplicatePredefinedCapabilities()
    {
        // Predefined capabilities should replace duplicates
        var capabilities = new Capabilities(false);
        capabilities.Add(new CapabilityValue(Capability.Model, "test1"));
        capabilities.Add(new CapabilityValue(Capability.Model, "test2"));

        var result = capabilities.ToString();
        Assert.AreEqual("Model=test2", result);
        Assert.DoesNotContain("test1", result);
    }
}
