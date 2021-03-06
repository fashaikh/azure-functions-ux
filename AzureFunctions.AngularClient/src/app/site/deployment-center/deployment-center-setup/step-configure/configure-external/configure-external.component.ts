import { Component } from '@angular/core';
import { SelectOption } from 'app/shared/models/select-option';
import { DeploymentCenterStateManager } from 'app/site/deployment-center/deployment-center-setup/wizard-logic/deployment-center-state-manager';

@Component({
    selector: 'app-configure-external',
    templateUrl: './configure-external.component.html',
    styleUrls: ['./configure-external.component.scss', '../step-configure.component.scss']
})
export class ConfigureExternalComponent {
    public RepoTypeOptions: SelectOption<string>[] = [
        {
            displayLabel: 'Mercurial',
            value: 'Mercurial'
        },
        {
            displayLabel: 'Git',
            value: 'Git'
        }
    ];
    public repoMode = 'Git';
    constructor(public wizard: DeploymentCenterStateManager) {}

    repoTypeChanged(evt) {
        this.repoMode = evt;
        this.wizard.wizardForm.controls.sourceSettings.value.isMercurial = evt === 'Mercurial';
        console.log(evt);
    }
}
