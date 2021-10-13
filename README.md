## Deployments into umbraco

### Config
Add the following object to the root of the appsettings.json file
```json
"Limbo": {
    "Emply": {
        "apiKey": "efe48574-7b2a-4a84-8557-adb89140acc6",
        "customerName": "toender",
        "mediaId": "30b0425b-09fe-4b34-8ba5-0d159b1b1cff",
        "category": "2da25e47-6839-439c-97a4-be2c65277fe3",
        "templateAlias": "job"
    }
}
```

### Adding configuration to the pool
Find your LimboOptions class if not existing create it with this code
```CSharp
public class LimboOptions
{
    public EmplyOptions Emply { get; set; }
}
```
Add this line to the startup.cs -> ConfigureServices method
```CSharp
services.Configure<LimboOptions>(options => _config.GetSection("Limbo").Bind(options));
```

Now the setting can be injected with
```CSharp
IOptions<LimboOptions>
```
The EmplyOptions class is found in this code just add the namespace to the LimboOptions.cs file

### Inside umbraco
Create template with the same alias as in the constants. Document type must have givent data ("job" is default)
* emplyId
* category
* categoryId
* placeOfEmpleyment
* webiste
* jobDeadline
* contact
* applicationLink

If non of those props are added or even one is missing, umbraco will throw an exception