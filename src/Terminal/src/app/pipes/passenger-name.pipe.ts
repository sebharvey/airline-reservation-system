import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'passengerName', standalone: true })
export class PassengerNamePipe implements PipeTransform {
  transform(passenger: { givenName?: string | null; surname?: string | null; title?: string | null } | null | undefined): string {
    if (!passenger) return '';
    const surname = (passenger.surname ?? '').toUpperCase().trim();
    const given = (passenger.givenName ?? '').toUpperCase().trim();
    if (!surname && !given) return '';
    const base = surname && given ? `${surname}/${given}` : surname || given;
    const title = (passenger.title ?? '').toUpperCase().trim();
    return title ? `${base} ${title}` : base;
  }
}
