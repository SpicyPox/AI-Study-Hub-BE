package com.java.aistudyhubbe.service;

import com.java.aistudyhubbe.dto.UserUpdateRequest;
import com.java.aistudyhubbe.entity.User;
import com.java.aistudyhubbe.exception.UserNotFoundException;
import com.java.aistudyhubbe.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.UUID;

@Service
@RequiredArgsConstructor
public class UserServiceImpl implements UserService {

    private final UserRepository userRepository;

    @Override
    public User saveUser(User user) {
        return userRepository.save(user);
    }

    @Override
    public User getUserById(UUID id) {
        return userRepository.findById(id)
                .orElseThrow(() -> new UserNotFoundException("User with ID " + id + " not found."));
    }

    @Override
    public List<User> getAllUsers() {
        return userRepository.findAll();
    }

    @Override
    public void deleteUser(UUID id) {
        if (!userRepository.existsById(id)) {
            throw new UserNotFoundException("User with ID " + id + " not found.");
        }
        userRepository.deleteById(id);
    }

    @Override
    public User getCurrentUser() {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication != null && authentication.getPrincipal() instanceof User) {
            return (User) authentication.getPrincipal();
        }
        throw new UserNotFoundException("Authenticated user not found in security context.");
    }

    @Override
    public User updateUserProfile(UserUpdateRequest request) {
        User currentUser = getCurrentUser();
        if (request.getEmail() != null && !request.getEmail().isEmpty()) {
            currentUser.setEmail(request.getEmail());
        }
        return userRepository.save(currentUser);
    }
}
