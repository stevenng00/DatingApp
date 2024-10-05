import { ActivatedRouteSnapshot, CanActivate, CanActivateFn, GuardResult, MaybeAsync, RouterStateSnapshot } from '@angular/router';
import { AccountService } from '../_services/account.service';
import { ToastrService } from 'ngx-toastr';
import { map, Observable } from 'rxjs';
import { inject, Injectable } from '@angular/core';



@Injectable({
  providedIn:'root'
})
export class AuthGuard implements CanActivate{
  constructor(private accountService: AccountService, private toastr: ToastrService){}

  canActivate(): Observable<boolean> {
    return this.accountService.currentUser$.pipe(
      map(user=>{
        if(user) return true;
        else {
          this.toastr.error('You shall not pass!');
          return false
        }
      })
    )
  }
}