using Heterodyna.ProtocolLab.Core;

try
{
    int count = CoreSelfTests.Run(Console.WriteLine);
    Console.WriteLine($"PASS: {count} testów / dane syntetyczne; testów RF i GUI nie wykonano.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
