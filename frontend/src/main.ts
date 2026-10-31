import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// Fiyat ve tarih biçimleri için Türkçe yerel ayar
registerLocaleData(localeTr);

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
