import {Component, OnInit, Input, EventEmitter,  Output } from '@angular/core';
import {GlobalStateService} from '../shared/services/global-state.service';
import {TranslateService, TranslatePipe} from 'ng2-translate/ng2-translate';
import {ApiProxy} from '../shared/models/api-proxy';
import {FunctionsService} from '../shared/services/functions.service';
import {FormBuilder, FormGroup, Validators, FormControl, ValidatorFn, AbstractControl } from '@angular/forms';
import {PortalResources} from '../shared/models/portal-resources';
import {BroadcastService} from '../shared/services/broadcast.service';
import {BroadcastEvent} from '../shared/models/broadcast-event';
import {ErrorEvent} from '../shared/models/error-event';

@Component({
  selector: 'api-new',
  templateUrl: './api-new.component.html',
  //styleUrls: ['./api-new.component.scss']
  styleUrls: ['./api-new.component.css', '../binding-input/binding-input.component.css']
})
export class ApiNewComponent implements OnInit {

    @Input() apiProxies: ApiProxy[];
    complexForm: FormGroup;
    isMethodsVisible: boolean = false;
    @Output() private functionAppSettingsClicked: EventEmitter<any> = new EventEmitter<any>();
    isEnabled: boolean;

    constructor(fb: FormBuilder,
        private _globalStateService: GlobalStateService,
        private _translateService: TranslateService,
        private _functionsService: FunctionsService,
        private _broadcastService: BroadcastService) {

        this.complexForm = fb.group({
            // We can set default values by passing in the corresponding value or leave blank if we wish to not set the value. For our example, we�ll default the gender to female.
            routeTemplate: '',
            methodSelectionType: 'All',
            name: [null, Validators.compose([Validators.required, this.validateName(this)])],
            backendUri: [null, Validators.required],
            method_get: false,
            method_post: false,
            method_delete: false,
            method_head: false,
            method_patch: false,
            method_put: false,
            method_options: false,
            method_trace: false
        });

        this.complexForm.controls["methodSelectionType"].valueChanges.subscribe((value) => {
            this.isMethodsVisible = !(value === 'All');
        });

        this.isEnabled = this._globalStateService.IsRoutingEnabled;
    }

    onFunctionAppSettingsClicked(event: any) {
        this.functionAppSettingsClicked.emit(event);
    }

    validateName(that: ApiNewComponent): ValidatorFn {

        return (control: AbstractControl): { [key: string]: any } => {
            var existingProxy = null;
            if (that.complexForm) {
                var name = control.value;
                if (that.apiProxies && name) {
                    existingProxy = that.apiProxies.find((p) => {
                        return p.name === name;
                    });
                }
            }

            return existingProxy ? {
                validateName: {
                    valid: false
                }
            } : null;
        };
    };

    ngOnInit() {
    }

    submitForm(value: any) {

        if (this.complexForm.valid) {
            this._globalStateService.setBusyState();

            var newApiProxy: ApiProxy = {
                name: this.complexForm.controls["name"].value,
                backendUri: this.complexForm.controls["backendUri"].value,
                matchCondition: {
                    route: this.complexForm.controls["routeTemplate"].value,
                    methods: []
                }
            };

            this._functionsService.getApiProxies().subscribe(proxies => {

                this.apiProxies = ApiProxy.fromJson(proxies);
                var existingProxy = this.apiProxies.find((p) => {
                    return p.name === newApiProxy.name;
                });

                if (existingProxy) {
                    this._globalStateService.clearBusyState();
                    this._broadcastService.broadcast<ErrorEvent>(BroadcastEvent.Error, { message: this._translateService.instant(PortalResources.apiProxy_alreadyExists, { name: newApiProxy.name }) });
                    throw `Proxy with name '${newApiProxy.name}' already exists`;
                } else {
                    if (this.complexForm.controls["methodSelectionType"].value !== "All") {
                        for (var control in this.complexForm.controls) {
                            if (control.startsWith("method_")) {
                                if (this.complexForm.controls[control].value) {
                                    newApiProxy.matchCondition.methods.push(control.replace("method_", "").toLocaleLowerCase());
                                }
                            }
                        }
                    }
                }

                this.apiProxies.push(newApiProxy);

                this._functionsService.saveApiProxy(ApiProxy.toJson(this.apiProxies)).subscribe(() => {
                    this._globalStateService.clearBusyState();
                    this._broadcastService.broadcast(BroadcastEvent.ApiProxyAdded, newApiProxy);
                });
            });

        }

    }

}