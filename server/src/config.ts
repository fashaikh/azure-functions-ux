type RuntimeVersion = '~1' | 'beta' | 'latest';
interface StaticConfig {
    config: {
        env: {
            runtimeType: 'Azure' | 'OnPrem' | 'Standalone';
            azureResourceManagerEndpoint: string;
            hostName: string | undefined;
        };
        cacheBreakQuery: string;
        isAzure: boolean;
        isOnPrem: boolean;
        clientOptimzationsOff: boolean;
        functionsVersionInfo: {
            runtimeStable: Array<RuntimeVersion>;
            runtimeDefault: RuntimeVersion;
        };
    };
}

export const staticConfig: StaticConfig = {
    config: {
        env: {
            runtimeType: 'Azure',
            azureResourceManagerEndpoint: 'https://management.azure.com',
            hostName: process.env.WEBSITE_HOSTNAME
        },
        cacheBreakQuery: "{{cacheBreakQuery}}",
        // TODO: [ehamai] I wouldn't use "isAzure" or "isOnPrem" as properties. RuntimeType should contain all of those variations.
        isAzure: !!process.env.WEBSITE_SITE_NAME,
        isOnPrem: false,
        clientOptimzationsOff: false,
        functionsVersionInfo: {
            runtimeStable: [ '~1', 'beta', 'latest' ],
            runtimeDefault: '~1'
        }
    }
};
